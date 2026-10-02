using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class RateLimiterConcurrencyTests
    {
        [Fact]
        public void TokenBucket_ConcurrentRequests_RespectsCapacityAndReplenishesCorrectly()
        {
            const int maxTokens = 10;
            const double tokensPerSecond = 100.0;
            const int totalThreads = 50;

            var policy = VardPolicy.TokenBucket(maxTokens, tokensPerSecond);

            int acceptedCount = 0;
            int rejectedCount = 0;
            var rejectedExceptions = new ConcurrentBag<RateLimiterRejectedException>();

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
                            Interlocked.Increment(ref acceptedCount);
                            return true;
                        });
                    }
                    catch (RateLimiterRejectedException ex)
                    {
                        Interlocked.Increment(ref rejectedCount);
                        rejectedExceptions.Add(ex);
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

            // Exactly 10 requests accepted (initial burst capacity)
            Assert.Equal(maxTokens, acceptedCount);
            // Remaining 40 requests rejected
            Assert.Equal(totalThreads - maxTokens, rejectedCount);

            // All rejected exceptions must provide a positive retry-after
            Assert.All(rejectedExceptions, ex =>
            {
                Assert.True(ex.RetryAfter > TimeSpan.Zero, "RetryAfter must be strictly positive");
                Assert.Equal(maxTokens, ex.PermitLimit);
                Assert.Equal("TokenBucket", ex.AlgorithmName);
            });

            Assert.True(policy.AvailablePermits >= 0, "AvailablePermits must never be negative");

            // Replenishment test: after waiting 50ms, at 100 tokens/sec, ~5 tokens should replenish
            Thread.Sleep(60);
            Assert.True(policy.AvailablePermits > 0, "Tokens should have replenished after elapsed time");

            bool canExecuteAfterReplenish = policy.Execute(() => true);
            Assert.True(canExecuteAfterReplenish);
        }

        [Fact]
        public void SlidingWindow_ConcurrentHammer_RespectsPermitLimitAcrossWindow()
        {
            const int permitLimit = 15;
            var windowDuration = TimeSpan.FromSeconds(2);
            const int segmentsPerWindow = 10;
            const int totalThreads = 50;

            var policy = VardPolicy.SlidingWindow(permitLimit, windowDuration, segmentsPerWindow);

            int acceptedCount = 0;
            int rejectedCount = 0;
            var rejectedExceptions = new ConcurrentBag<RateLimiterRejectedException>();

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
                            Interlocked.Increment(ref acceptedCount);
                            return 42;
                        });
                    }
                    catch (RateLimiterRejectedException ex)
                    {
                        Interlocked.Increment(ref rejectedCount);
                        rejectedExceptions.Add(ex);
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

            // Exactly 15 requests accepted within the time window
            Assert.Equal(permitLimit, acceptedCount);
            // Exactly 35 requests rejected
            Assert.Equal(totalThreads - permitLimit, rejectedCount);

            // All rejected exceptions must contain positive RetryAfter and correct algorithm name
            Assert.All(rejectedExceptions, ex =>
            {
                Assert.True(ex.RetryAfter > TimeSpan.Zero);
                Assert.Equal(permitLimit, ex.PermitLimit);
                Assert.Equal("SlidingWindow", ex.AlgorithmName);
            });

            Assert.Equal(0, policy.AvailablePermits);
        }

        [Fact]
        public void RateLimiter_ConcurrentMultiPermitRequests_MaintainsPermitAccounting()
        {
            const int maxTokens = 20;
            // 1 token per second to ensure no extra tokens replenish during instantaneous burst execution
            var policy = VardPolicy.TokenBucket(maxTokens, 1.0);

            const int totalThreads = 20;
            int acceptedCount = 0;
            int rejectedCount = 0;
            int totalPermitsConsumed = 0;

            var barrier = new Barrier(totalThreads);
            var threads = new Thread[totalThreads];

            for (int i = 0; i < totalThreads; i++)
            {
                // Half request 2 permits, half request 3 permits (total attempted: 10*2 + 10*3 = 50 permits)
                int cost = (i % 2 == 0) ? 2 : 3;
                var context = new Dictionary<string, object>
                {
                    [RateLimiterContextKeys.Cost] = cost
                };

                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();

                    try
                    {
                        policy.Execute(ctx =>
                        {
                            Interlocked.Increment(ref acceptedCount);
                            Interlocked.Add(ref totalPermitsConsumed, cost);
                            return true;
                        }, context);
                    }
                    catch (RateLimiterRejectedException)
                    {
                        Interlocked.Increment(ref rejectedCount);
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

            // Permit consumption must never exceed total available permits (20)
            Assert.True(totalPermitsConsumed <= maxTokens, $"Consumed permits {totalPermitsConsumed} exceeded max {maxTokens}");
            Assert.Equal(totalThreads, acceptedCount + rejectedCount);
            Assert.True(rejectedCount > 0, "Requests exceeding remaining permits must be rejected");

            // Accounting integrity: Available permits + consumed permits must equal maxTokens
            Assert.Equal(maxTokens - totalPermitsConsumed, policy.AvailablePermits);
        }

        [Fact]
        public async Task RateLimiter_AsyncConcurrentExecution_ThreadSafety()
        {
            const int totalTasks = 60;
            var tokenBucketPolicy = VardPolicy.TokenBucket(20, 50.0);
            var slidingWindowPolicy = VardPolicy.SlidingWindow(20, TimeSpan.FromSeconds(1));

            int tbSuccess = 0;
            int tbRejected = 0;
            int swSuccess = 0;
            int swRejected = 0;

            var tasks = Enumerable.Range(0, totalTasks).Select(async i =>
            {
                await Task.Yield();

                // Test TokenBucket async under contention
                try
                {
                    await tokenBucketPolicy.ExecuteAsync(async ct =>
                    {
                        await Task.Delay(2, ct);
                        return i;
                    });
                    Interlocked.Increment(ref tbSuccess);
                }
                catch (RateLimiterRejectedException)
                {
                    Interlocked.Increment(ref tbRejected);
                }

                // Test SlidingWindow async under contention
                try
                {
                    await slidingWindowPolicy.ExecuteAsync(async ct =>
                    {
                        await Task.Delay(2, ct);
                        return i;
                    });
                    Interlocked.Increment(ref swSuccess);
                }
                catch (RateLimiterRejectedException)
                {
                    Interlocked.Increment(ref swRejected);
                }
            });

            await Task.WhenAll(tasks);

            Assert.Equal(totalTasks, tbSuccess + tbRejected);
            Assert.Equal(totalTasks, swSuccess + swRejected);
            Assert.True(tbSuccess > 0, "TokenBucket should have accepted requests");
            Assert.True(swSuccess > 0, "SlidingWindow should have accepted requests");
            Assert.True(tbRejected > 0, "TokenBucket should have rejected requests beyond burst");
            Assert.True(swRejected > 0, "SlidingWindow should have rejected requests beyond window quota");

            Assert.True(tokenBucketPolicy.AvailablePermits >= 0);
            Assert.True(slidingWindowPolicy.AvailablePermits >= 0);
        }

        [Fact]
        public async Task RateLimiter_ExecuteAndCaptureAsync_ThreadSafety_CapturesCorrectOutcomes()
        {
            const int totalTasks = 40;
            var policy = VardPolicy.TokenBucket(15, 10.0);

            var tasks = Enumerable.Range(0, totalTasks).Select(async i =>
            {
                await Task.Yield();
                return await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    await Task.Delay(5, ct);
                    return i * 10;
                });
            });

            var results = await Task.WhenAll(tasks);

            int successes = results.Count(r => r.IsSuccess);
            int rejections = results.Count(r => !r.IsSuccess && r.FinalException is RateLimiterRejectedException);

            Assert.Equal(totalTasks, successes + rejections);
            Assert.Equal(15, successes);
            Assert.Equal(25, rejections);
        }
    }
}
