using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class SlidingWindowRateLimiterPolicyTests
    {
        [Fact]
        public void Constructor_InvalidParameters_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(0, TimeSpan.FromSeconds(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(-1, TimeSpan.FromSeconds(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(5, TimeSpan.Zero));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(5, TimeSpan.FromSeconds(-1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(5, TimeSpan.FromSeconds(1), segmentsPerWindow: 0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.SlidingWindow(5, TimeSpan.FromSeconds(1), maxWaitTime: TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Properties_InitialState_ReflectsConfiguration()
        {
            var policy = VardPolicy.SlidingWindow(10, TimeSpan.FromSeconds(5));

            Assert.Equal(10, policy.PermitLimit);
            Assert.Equal(10, policy.AvailablePermits);
            Assert.Equal("SlidingWindow", policy.AlgorithmName);
        }

        [Fact]
        public void Execute_UnderLimit_ExecutesSuccessfully()
        {
            var policy = VardPolicy.SlidingWindow(5, TimeSpan.FromSeconds(10));

            for (int i = 0; i < 5; i++)
            {
                int result = policy.Execute(() => i * 3);
                Assert.Equal(i * 3, result);
            }

            Assert.Equal(0, policy.AvailablePermits);
        }

        [Fact]
        public void Execute_ExceedingLimit_ThrowsRateLimiterRejectedException_WithDynamicRetryAfter()
        {
            var policy = VardPolicy.SlidingWindow(3, TimeSpan.FromSeconds(10));

            for (int i = 0; i < 3; i++)
            {
                policy.Execute(() => true);
            }

            var ex = Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(() => true));

            Assert.Equal(3, ex.PermitLimit);
            Assert.Equal("SlidingWindow", ex.AlgorithmName);
            Assert.True(ex.RetryAfter > TimeSpan.Zero);
            Assert.Contains("exceeded", ex.Message);
        }

        [Fact]
        public void Execute_AfterWindowExpires_AllowsNewExecutions()
        {
            long currentTicks = 1000L * TimeSpan.TicksPerSecond;
            var windowDuration = TimeSpan.FromSeconds(10);
            var policy = new SlidingWindowRateLimiterPolicy(
                permitLimit: 2,
                windowDuration: windowDuration,
                segmentsPerWindow: 10,
                ticksProvider: () => currentTicks);

            Assert.Equal(2, policy.AvailablePermits);

            // Consume 2 permits
            policy.Execute(() => 1);
            policy.Execute(() => 2);
            Assert.Equal(0, policy.AvailablePermits);

            // Rejects immediately
            Assert.Throws<RateLimiterRejectedException>(() => policy.Execute(() => 3));

            // Advance time past the entire window + bucket duration
            currentTicks += windowDuration.Ticks + TimeSpan.TicksPerSecond * 2;

            Assert.Equal(2, policy.AvailablePermits);

            // Can execute again
            int result = policy.Execute(() => 4);
            Assert.Equal(4, result);
            Assert.Equal(1, policy.AvailablePermits);
        }

        [Fact]
        public void Execute_WithMaxWaitTime_WaitsAndSucceeds()
        {
            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromMilliseconds(50),
                segmentsPerWindow: 5,
                maxWaitTime: TimeSpan.FromMilliseconds(300));

            // Consume first permit
            policy.Execute(() => 1);

            // Second execution waits ~50ms and succeeds
            int result = policy.Execute(() => 2);
            Assert.Equal(2, result);
        }

        [Fact]
        public void Execute_WithMaxWaitTime_ExceedingWaitTime_RejectsImmediately()
        {
            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromSeconds(10),
                maxWaitTime: TimeSpan.FromMilliseconds(50));

            policy.Execute(() => 1);

            // RetryAfter will be ~10s, maxWaitTime is 50ms -> rejects immediately
            var ex = Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(() => 2));

            Assert.Equal(1, ex.PermitLimit);
            Assert.True(ex.RetryAfter > TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task ExecuteAsync_WithMaxWaitTime_WaitsAndSucceeds()
        {
            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromMilliseconds(50),
                segmentsPerWindow: 5,
                maxWaitTime: TimeSpan.FromMilliseconds(300));

            await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 1;
            });

            int result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 2;
            });

            Assert.Equal(2, result);
        }

        [Fact]
        public async Task Execute_CancellationDuringWait_ThrowsOperationCanceledException_WithoutConsumingOrCallingCallback()
        {
            bool callbackInvoked = false;
            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromSeconds(5),
                maxWaitTime: TimeSpan.FromSeconds(10),
                onRateLimitExceeded: (retryAfter, ctx) => callbackInvoked = true);

            // Consume initial permit
            await policy.ExecuteAsync(async ct => await Task.FromResult(1));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    return 2;
                }, cts.Token);
            });

            Assert.False(callbackInvoked);
        }

        [Fact]
        public void Execute_VariablePermitCost_ConsumesMultiplePermits()
        {
            var policy = VardPolicy.SlidingWindow(10, TimeSpan.FromSeconds(5));

            var context1 = new Dictionary<string, object>
            {
                [RateLimiterContextKeys.Cost] = 4
            };

            policy.Execute(ctx => "ok1", context1);
            Assert.Equal(6, policy.AvailablePermits);

            // Request cost 7 -> 6 + 7 = 13 > 10, rejects
            var context2 = new Dictionary<string, object>
            {
                [RateLimiterContextKeys.Cost] = 7
            };

            Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(ctx => "fail", context2));

            // Available permits is still 6
            Assert.Equal(6, policy.AvailablePermits);

            // Request cost 6 -> fits exactly
            var context3 = new Dictionary<string, object>
            {
                [RateLimiterContextKeys.Cost] = 6
            };
            policy.Execute(ctx => "ok2", context3);
            Assert.Equal(0, policy.AvailablePermits);
        }

        [Fact]
        public void Execute_ObservabilityCallbacks_InvokedWithContext()
        {
            TimeSpan capturedRetryAfter = TimeSpan.Zero;
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromSeconds(2),
                onRateLimitExceeded: (retryAfter, ctx) =>
                {
                    capturedRetryAfter = retryAfter;
                    capturedContext = ctx;
                });

            var context = new Dictionary<string, object> { ["TenantId"] = "tenant-abc" };

            // First succeeds
            policy.Execute(ctx => 1, context);

            // Second rejects
            Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(ctx => 2, context));

            Assert.True(capturedRetryAfter > TimeSpan.Zero);
            Assert.NotNull(capturedContext);
            Assert.Equal("tenant-abc", capturedContext!["TenantId"]);
        }

        [Fact]
        public async Task ExecuteAsync_AsyncObservabilityCallback_InvokedWithContext()
        {
            TimeSpan capturedRetryAfter = TimeSpan.Zero;
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.SlidingWindow(
                permitLimit: 1,
                windowDuration: TimeSpan.FromSeconds(2),
                onRateLimitExceededAsync: async (retryAfter, ctx) =>
                {
                    await Task.Yield();
                    capturedRetryAfter = retryAfter;
                    capturedContext = ctx;
                });

            var context = new Dictionary<string, object> { ["CorrelationId"] = "corr-xyz" };

            await policy.ExecuteAsync(async (ctx, ct) => await Task.FromResult(1), context);

            await Assert.ThrowsAsync<RateLimiterRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async (ctx, ct) => await Task.FromResult(2), context);
            });

            Assert.True(capturedRetryAfter > TimeSpan.Zero);
            Assert.NotNull(capturedContext);
            Assert.Equal("corr-xyz", capturedContext!["CorrelationId"]);
        }

        [Fact]
        public void ExecuteAndCapture_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            var policy = VardPolicy.SlidingWindow(1, TimeSpan.FromSeconds(2));

            var result1 = policy.ExecuteAndCapture(() => "first");
            Assert.True(result1.IsSuccess);
            Assert.Equal("first", result1.Result);

            var result2 = policy.ExecuteAndCapture(() => "second");
            Assert.False(result2.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result2.ExceptionType);
            Assert.IsType<RateLimiterRejectedException>(result2.FinalException);
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            var policy = VardPolicy.SlidingWindow(1, TimeSpan.FromSeconds(2));

            var result1 = await policy.ExecuteAndCaptureAsync(async ct => await Task.FromResult(10));
            Assert.True(result1.IsSuccess);

            var result2 = await policy.ExecuteAndCaptureAsync(async ct => await Task.FromResult(20));
            Assert.False(result2.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result2.ExceptionType);
            Assert.IsType<RateLimiterRejectedException>(result2.FinalException);
        }

        [Fact]
        public void BuilderExtensions_ConfigureSlidingWindow_Correctly()
        {
            var policy = VardPolicy.Handle<Exception>()
                .SlidingWindowRateLimiter(8, TimeSpan.FromSeconds(3), segmentsPerWindow: 6);

            Assert.Equal(8, policy.PermitLimit);
            Assert.Equal("SlidingWindow", policy.AlgorithmName);
        }
    }
}
