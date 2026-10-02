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
    public class TokenBucketRateLimiterPolicyTests
    {
        [Fact]
        public void Constructor_InvalidParameters_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.TokenBucket(0, 10));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.TokenBucket(-1, 10));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.TokenBucket(5, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.TokenBucket(5, -1));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.TokenBucket(5, 10, TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Properties_InitialState_ReflectsConfiguration()
        {
            var policy = VardPolicy.TokenBucket(10, 5.0);

            Assert.Equal(10, policy.PermitLimit);
            Assert.Equal(10, policy.AvailablePermits);
            Assert.Equal("TokenBucket", policy.AlgorithmName);
        }

        [Fact]
        public void Execute_UnderLimit_ExecutesSuccessfully()
        {
            var policy = VardPolicy.TokenBucket(5, 1.0);

            for (int i = 0; i < 5; i++)
            {
                int result = policy.Execute(() => i * 2);
                Assert.Equal(i * 2, result);
            }

            Assert.Equal(0, policy.AvailablePermits);
        }

        [Fact]
        public void Execute_BurstExceeded_ThrowsRateLimiterRejectedException_WithRetryAfter()
        {
            var policy = VardPolicy.TokenBucket(3, 2.0);

            // Exhaust all 3 tokens
            for (int i = 0; i < 3; i++)
            {
                policy.Execute(() => true);
            }

            var ex = Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(() => true));

            Assert.Equal(3, ex.PermitLimit);
            Assert.Equal("TokenBucket", ex.AlgorithmName);
            Assert.True(ex.RetryAfter > TimeSpan.Zero);
            Assert.Contains("exceeded", ex.Message);
        }

        [Fact]
        public void Execute_ReplenishesTokens_WithSimulatedTime()
        {
            long currentTicks = 1000L * TimeSpan.TicksPerSecond;
            var policy = new TokenBucketRateLimiterPolicy(
                maxTokens: 2,
                tokensPerSecond: 1.0,
                ticksProvider: () => currentTicks);

            Assert.Equal(2, policy.AvailablePermits);

            // Consume 2 tokens
            policy.Execute(() => true);
            policy.Execute(() => true);
            Assert.Equal(0, policy.AvailablePermits);

            // Rejection immediate
            Assert.Throws<RateLimiterRejectedException>(() => policy.Execute(() => true));

            // Advance time by 1 second -> replenishes 1 token
            currentTicks += TimeSpan.TicksPerSecond;
            Assert.Equal(1, policy.AvailablePermits);

            // Can execute 1 more
            bool executed = policy.Execute(() => true);
            Assert.True(executed);
            Assert.Equal(0, policy.AvailablePermits);

            // Advance time by 10 seconds -> capped at maxTokens (2)
            currentTicks += 10 * TimeSpan.TicksPerSecond;
            Assert.Equal(2, policy.AvailablePermits);
        }

        [Fact]
        public void Execute_WithMaxWaitTime_WaitsAndSucceeds()
        {
            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 100.0, // 1 token every 10ms
                maxWaitTime: TimeSpan.FromMilliseconds(200));

            // Consume first token
            policy.Execute(() => 1);

            // Second execution waits ~10ms and succeeds
            int result = policy.Execute(() => 2);
            Assert.Equal(2, result);
        }

        [Fact]
        public void Execute_WithMaxWaitTime_ExceedingWaitTime_RejectsImmediately()
        {
            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 1.0, // 1 token every 1s
                maxWaitTime: TimeSpan.FromMilliseconds(50));

            policy.Execute(() => 1);

            // Requires 1s wait, but maxWaitTime is 50ms -> rejects immediately
            var ex = Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(() => 2));

            Assert.Equal(1, ex.PermitLimit);
            Assert.True(ex.RetryAfter > TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task ExecuteAsync_WithMaxWaitTime_WaitsAndSucceeds()
        {
            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 100.0,
                maxWaitTime: TimeSpan.FromMilliseconds(200));

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
            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 0.5, // 2 seconds per token
                maxWaitTime: TimeSpan.FromSeconds(5),
                onRateLimitExceeded: (retryAfter, ctx) => callbackInvoked = true);

            // Consume initial token
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
        public void Execute_VariablePermitCost_ConsumesConfiguredPermits()
        {
            var policy = VardPolicy.TokenBucket(10, 1.0);

            var context = new Dictionary<string, object>
            {
                [RateLimiterContextKeys.Cost] = 6
            };

            policy.Execute(ctx => "ok", context);
            Assert.Equal(4, policy.AvailablePermits);

            // Next call requests cost 5, but only 4 available
            var context2 = new Dictionary<string, object>
            {
                [RateLimiterContextKeys.Cost] = 5
            };

            Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(ctx => "fail", context2));
        }

        [Fact]
        public void Execute_ObservabilityCallbacks_InvokedOnRejection_WithRetryAfterAndContext()
        {
            TimeSpan capturedRetryAfter = TimeSpan.Zero;
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 1.0,
                onRateLimitExceeded: (retryAfter, ctx) =>
                {
                    capturedRetryAfter = retryAfter;
                    capturedContext = ctx;
                });

            var context = new Dictionary<string, object> { ["UserId"] = "usr-123" };

            // First call succeeds
            policy.Execute(ctx => 1, context);

            // Second call rejected
            Assert.Throws<RateLimiterRejectedException>(() =>
                policy.Execute(ctx => 2, context));

            Assert.True(capturedRetryAfter > TimeSpan.Zero);
            Assert.NotNull(capturedContext);
            Assert.Equal("usr-123", capturedContext!["UserId"]);
        }

        [Fact]
        public async Task ExecuteAsync_AsyncObservabilityCallback_InvokedOnRejection()
        {
            TimeSpan capturedRetryAfter = TimeSpan.Zero;
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.TokenBucket(
                maxTokens: 1,
                tokensPerSecond: 1.0,
                onRateLimitExceededAsync: async (retryAfter, ctx) =>
                {
                    await Task.Yield();
                    capturedRetryAfter = retryAfter;
                    capturedContext = ctx;
                });

            var context = new Dictionary<string, object> { ["RequestId"] = "req-999" };

            await policy.ExecuteAsync(async (ctx, ct) => await Task.FromResult(1), context);

            await Assert.ThrowsAsync<RateLimiterRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async (ctx, ct) => await Task.FromResult(2), context);
            });

            Assert.True(capturedRetryAfter > TimeSpan.Zero);
            Assert.NotNull(capturedContext);
            Assert.Equal("req-999", capturedContext!["RequestId"]);
        }

        [Fact]
        public void ExecuteAndCapture_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            var policy = VardPolicy.TokenBucket(1, 1.0);

            // First succeeds
            var result1 = policy.ExecuteAndCapture(() => 42);
            Assert.True(result1.IsSuccess);
            Assert.Equal(42, result1.Result);

            // Second is rejected
            var result2 = policy.ExecuteAndCapture(() => 99);
            Assert.False(result2.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result2.ExceptionType);
            Assert.IsType<RateLimiterRejectedException>(result2.FinalException);
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_WhenRejected_ReturnsPolicyResult_PolicyBypassed()
        {
            var policy = VardPolicy.TokenBucket(1, 1.0);

            var result1 = await policy.ExecuteAndCaptureAsync(async ct => await Task.FromResult("a"));
            Assert.True(result1.IsSuccess);

            var result2 = await policy.ExecuteAndCaptureAsync(async ct => await Task.FromResult("b"));
            Assert.False(result2.IsSuccess);
            Assert.Equal(ExceptionType.PolicyBypassed, result2.ExceptionType);
            Assert.IsType<RateLimiterRejectedException>(result2.FinalException);
        }

        [Fact]
        public void BuilderExtensions_ConfigureTokenBucket_Correctly()
        {
            var policy = VardPolicy.Handle<Exception>()
                .TokenBucketRateLimiter(5, 10.0);

            Assert.Equal(5, policy.PermitLimit);
            Assert.Equal("TokenBucket", policy.AlgorithmName);
        }
    }
}
