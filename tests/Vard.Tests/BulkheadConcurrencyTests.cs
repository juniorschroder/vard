using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class BulkheadConcurrencyTests
    {
        [Fact]
        public void Bulkhead_ConcurrentExecution_NeverExceedsMaxParallelization()
        {
            const int maxParallel = 5;
            const int maxQueue = 50;
            const int totalThreads = 50;

            using var policy = VardPolicy.Bulkhead(maxParallel, maxQueue);

            int currentInFlight = 0;
            int maxObservedInFlight = 0;
            int totalSucceeded = 0;

            var barrier = new Barrier(totalThreads);
            var threads = new Thread[totalThreads];

            for (int i = 0; i < totalThreads; i++)
            {
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();

                    policy.Execute(() =>
                    {
                        int inFlight = Interlocked.Increment(ref currentInFlight);

                        int initialMax, newMax;
                        do
                        {
                            initialMax = maxObservedInFlight;
                            newMax = Math.Max(initialMax, inFlight);
                        } while (initialMax < newMax && Interlocked.CompareExchange(ref maxObservedInFlight, newMax, initialMax) != initialMax);

                        Thread.Sleep(15);

                        Interlocked.Decrement(ref currentInFlight);
                        Interlocked.Increment(ref totalSucceeded);
                        return true;
                    });
                })
                {
                    IsBackground = true
                };
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(totalThreads, totalSucceeded);
            Assert.True(maxObservedInFlight <= maxParallel, $"Max observed in flight {maxObservedInFlight} exceeded {maxParallel}");
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);
        }

        [Fact]
        public void Bulkhead_QueueLimit_AccuratelyRejectsOverflow_WithoutDeadlock()
        {
            const int maxParallel = 5;
            const int maxQueue = 10;
            const int totalThreads = 50;

            using var policy = VardPolicy.Bulkhead(maxParallel, maxQueue);

            int successCount = 0;
            int rejectionCount = 0;

            var barrier = new Barrier(totalThreads);
            var threads = new Thread[totalThreads];

            for (int i = 0; i < totalThreads; i++)
            {
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();

                    try
                    {
                        policy.Execute(() =>
                        {
                            Thread.Sleep(80);
                            Interlocked.Increment(ref successCount);
                            return 1;
                        });
                    }
                    catch (BulkheadRejectedException)
                    {
                        Interlocked.Increment(ref rejectionCount);
                    }
                })
                {
                    IsBackground = true
                };
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Exactly 15 operations execute successfully (5 parallel + 10 queued)
            Assert.Equal(maxParallel + maxQueue, successCount);
            // Exactly 35 operations are rejected
            Assert.Equal(totalThreads - (maxParallel + maxQueue), rejectionCount);

            // Zero semaphore leaks
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task Bulkhead_ConcurrentCancellations_DoNotLeakQueueOrExecutionSlots()
        {
            const int maxParallel = 2;
            const int maxQueue = 10;

            using var policy = VardPolicy.Bulkhead(maxParallel, maxQueue);

            using var executionGate = new ManualResetEventSlim(false);
            using var activeEnteredGate = new CountdownEvent(maxParallel);

            // Fill the 2 parallel slots with long-running tasks
            var activeTasks = Enumerable.Range(0, maxParallel).Select(_ => Task.Run(() =>
            {
                policy.Execute(() =>
                {
                    activeEnteredGate.Signal();
                    executionGate.Wait(TimeSpan.FromSeconds(5));
                    return true;
                });
            })).ToArray();

            Assert.True(activeEnteredGate.Wait(TimeSpan.FromSeconds(5)), "Active tasks should have entered execution");
            Assert.Equal(0, policy.BulkheadAvailableCount);

            // Dispatch 10 tasks into the queue with cancellation tokens
            var ctsList = Enumerable.Range(0, maxQueue).Select(_ => new CancellationTokenSource()).ToArray();
            var queuedTasks = new Task[maxQueue];

            for (int i = 0; i < maxQueue; i++)
            {
                var cts = ctsList[i];
                queuedTasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        await policy.ExecuteAsync(async ct =>
                        {
                            await Task.Delay(50, ct);
                            return true;
                        }, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                });
            }

            // Wait until all 10 are queued
            var spin = new SpinWait();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (policy.QueueAvailableCount > 0 && sw.ElapsedMilliseconds < 3000)
            {
                spin.SpinOnce();
            }
            Assert.Equal(0, policy.QueueAvailableCount);

            // Cancel all queued tasks concurrently
            Parallel.ForEach(ctsList, cts => cts.Cancel());

            // Wait for all cancelled queued tasks to complete
            await Task.WhenAll(queuedTasks);

            // The queue slots must be released immediately upon cancellation
            Assert.Equal(maxQueue, policy.QueueAvailableCount);

            // Release active tasks
            executionGate.Set();
            await Task.WhenAll(activeTasks);

            // Both execution slots must now be available
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);

            // Verify new tasks can execute without hindrance
            var postCancelTask1 = policy.Execute(() => 100);
            var postCancelTask2 = await policy.ExecuteAsync(async _ =>
            {
                await Task.Yield();
                return 200;
            });

            Assert.Equal(100, postCancelTask1);
            Assert.Equal(200, postCancelTask2);
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);

            foreach (var cts in ctsList) cts.Dispose();
        }

        [Fact]
        public async Task Bulkhead_AsyncExecutionUnderHeavyContention_ThreadSafety()
        {
            const int maxParallel = 10;
            const int maxQueue = 20;
            const int totalTasks = 100;

            using var policy = VardPolicy.Bulkhead(maxParallel, maxQueue);

            int successCount = 0;
            int rejectionCount = 0;

            var tasks = Enumerable.Range(0, totalTasks).Select(async i =>
            {
                await Task.Yield();
                try
                {
                    int res = await policy.ExecuteAsync(async ct =>
                    {
                        await Task.Delay(10, ct);
                        return i;
                    });
                    Interlocked.Increment(ref successCount);
                }
                catch (BulkheadRejectedException)
                {
                    Interlocked.Increment(ref rejectionCount);
                }
            });

            await Task.WhenAll(tasks);

            Assert.Equal(totalTasks, successCount + rejectionCount);
            Assert.True(successCount > 0, "At least some tasks should have succeeded");
            Assert.True(rejectionCount > 0, "Some tasks should have been rejected under contention");

            // All resources must be completely released
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);
        }

        [Fact]
        public async Task Bulkhead_ExecuteAndCaptureAsync_ConcurrentStress_ZeroLeaks()
        {
            const int maxParallel = 5;
            const int maxQueue = 10;
            const int totalTasks = 60;

            using var policy = VardPolicy.Bulkhead(maxParallel, maxQueue);

            var tasks = Enumerable.Range(0, totalTasks).Select(async i =>
            {
                await Task.Yield();
                return await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    await Task.Delay(10, ct);
                    return i * 2;
                });
            });

            var results = await Task.WhenAll(tasks);

            int successes = results.Count(r => r.IsSuccess);
            int rejections = results.Count(r => !r.IsSuccess && r.FinalException is BulkheadRejectedException);

            Assert.Equal(totalTasks, successes + rejections);
            Assert.Equal(maxParallel, policy.BulkheadAvailableCount);
            Assert.Equal(maxQueue, policy.QueueAvailableCount);
        }
    }
}
