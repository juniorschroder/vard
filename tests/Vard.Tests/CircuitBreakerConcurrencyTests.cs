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
    public class CircuitBreakerConcurrencyTests
    {
        [Fact]
        public async Task HalfOpen_ConcurrentRequests_OnlyOnePilotExecutes()
        {
            // Circuit trips after 1 failure, break duration 50ms
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromMilliseconds(50));

            // Trip breaker
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("trip")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            // Wait for break duration to pass
            await Task.Delay(75);

            int pilotExecutionCount = 0;
            int halfOpenRejectedCount = 0;
            int totalConcurrentTasks = 50;

            var barrier = new Barrier(totalConcurrentTasks);
            var tasks = new Task[totalConcurrentTasks];

            for (int i = 0; i < totalConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    // Synchronize all threads so they release at the exact same moment
                    barrier.SignalAndWait();

                    try
                    {
                        policy.Execute(() =>
                        {
                            Interlocked.Increment(ref pilotExecutionCount);
                            // Simulate small work in pilot
                            Thread.Sleep(20);
                            return 1;
                        });
                    }
                    catch (CircuitBreakerOpenException ex) when (ex.State == CircuitState.HalfOpen)
                    {
                        Interlocked.Increment(ref halfOpenRejectedCount);
                    }
                    catch (CircuitBreakerOpenException ex) when (ex.State == CircuitState.Open)
                    {
                        // In case someone arrived before break expired or race
                        Interlocked.Increment(ref halfOpenRejectedCount);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Exactly 1 pilot execution must have taken place!
            Assert.Equal(1, pilotExecutionCount);
            // The remaining 49 threads must have been rejected
            Assert.Equal(totalConcurrentTasks - 1, halfOpenRejectedCount);
            // After pilot succeeded, circuit state must be Closed
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public async Task Advanced_HalfOpen_ConcurrentRequests_OnlyOnePilotExecutes()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromMilliseconds(50));

            // Trip breaker
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("1")));
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("2")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(75);

            int pilotExecutionCount = 0;
            int halfOpenRejectedCount = 0;
            int totalConcurrentTasks = 50;

            var barrier = new Barrier(totalConcurrentTasks);
            var tasks = new Task[totalConcurrentTasks];

            for (int i = 0; i < totalConcurrentTasks; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();

                    try
                    {
                        policy.Execute(() =>
                        {
                            Interlocked.Increment(ref pilotExecutionCount);
                            Thread.Sleep(20);
                            return 100;
                        });
                    }
                    catch (CircuitBreakerOpenException ex) when (ex.State == CircuitState.HalfOpen || ex.State == CircuitState.Open)
                    {
                        Interlocked.Increment(ref halfOpenRejectedCount);
                    }
                });
            }

            await Task.WhenAll(tasks);

            Assert.Equal(1, pilotExecutionCount);
            Assert.Equal(totalConcurrentTasks - 1, halfOpenRejectedCount);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public async Task ConcurrentFailures_CountBased_TripsAtomicallyWithoutRaceCondition()
        {
            int breakCount = 0;
            var policy = VardPolicy.CircuitBreaker(
                5,
                TimeSpan.FromSeconds(10),
                onBreak: (_, _, _) => Interlocked.Increment(ref breakCount));

            int totalTasks = 30;
            var barrier = new Barrier(totalTasks);
            var tasks = new Task[totalTasks];

            for (int i = 0; i < totalTasks; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();

                    try
                    {
                        policy.Execute(() => throw new InvalidOperationException("fail"));
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected before breaker trips
                    }
                    catch (CircuitBreakerOpenException)
                    {
                        // Expected after breaker trips
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Breaker must be Open
            Assert.Equal(CircuitState.Open, policy.CircuitState);
            // OnBreak must fire exactly once when reaching threshold
            Assert.Equal(1, breakCount);
        }

        [Fact]
        public async Task ConcurrentHammer_AdvancedCircuitBreaker_ThreadSafeSlidingWindow()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.6,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 20,
                durationOfBreak: TimeSpan.FromSeconds(5));

            int totalTasks = 40;
            var barrier = new Barrier(totalTasks);
            var tasks = new Task[totalTasks];

            var successCounter = 0;
            var failureCounter = 0;
            var openCounter = 0;

            for (int i = 0; i < totalTasks; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();

                    for (int j = 0; j < 10; j++)
                    {
                        try
                        {
                            // 75% of operations fail
                            if ((index + j) % 4 != 0)
                            {
                                policy.Execute(() => throw new InvalidOperationException("err"));
                            }
                            else
                            {
                                policy.Execute(() => 42);
                                Interlocked.Increment(ref successCounter);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            Interlocked.Increment(ref failureCounter);
                        }
                        catch (CircuitBreakerOpenException)
                        {
                            Interlocked.Increment(ref openCounter);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Breaker should have tripped given high failure rate and high throughput
            Assert.Equal(CircuitState.Open, policy.CircuitState);
            Assert.True(openCounter > 0, "Some requests should have been rejected while breaker was open");
        }

        [Fact]
        public async Task ConcurrentHammer_AsyncExecution_ThreadSafety()
        {
            var policy = VardPolicy.CircuitBreaker(
                10,
                TimeSpan.FromSeconds(2));

            int totalTasks = 30;
            var tasks = Enumerable.Range(0, totalTasks).Select(async i =>
            {
                await Task.Yield();
                for (int j = 0; j < 15; j++)
                {
                    try
                    {
                        if (i % 2 == 0)
                        {
                            await policy.ExecuteAsync(async ct =>
                            {
                                await Task.Yield();
                                return i * j;
                            });
                        }
                        else
                        {
                            await policy.ExecuteAsync<int>(async ct =>
                            {
                                await Task.Yield();
                                throw new InvalidOperationException("async failure");
                            });
                        }
                    }
                    catch (InvalidOperationException) { }
                    catch (CircuitBreakerOpenException) { }
                }
            });

            await Task.WhenAll(tasks);

            // No unhandled exceptions, deadlock, or corruption
            Assert.True(policy.CircuitState == CircuitState.Open || policy.CircuitState == CircuitState.Closed);
        }

        [Fact]
        public async Task Concurrent_IsolateAndReset_UnderHeavyLoad()
        {
            var policy = VardPolicy.CircuitBreaker(5, TimeSpan.FromSeconds(5));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
            var token = cts.Token;

            // Worker tasks hammering the breaker
            var workers = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        policy.Execute(() => 1);
                    }
                    catch (CircuitBreakerOpenException)
                    {
                        // Expected when isolated or open
                    }
                }
            })).ToArray();

            // Controller task toggling Isolate and Reset
            var controller = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    policy.Isolate();
                    await Task.Delay(10);
                    policy.Reset();
                    await Task.Delay(10);
                }
            });

            await Task.WhenAll(workers.Concat(new[] { controller }));

            // Ensure finally clean state after reset
            policy.Reset();
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            int res = policy.Execute(() => 42);
            Assert.Equal(42, res);
        }
    }
}
