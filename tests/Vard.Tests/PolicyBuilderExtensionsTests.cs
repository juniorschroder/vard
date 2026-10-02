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
    public class PolicyBuilderExtensionsTests
    {
        [Fact]
        public void Builder_Retry_ExecutesAndRetries()
        {
            int attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Retry(2);

            string result = policy.Execute(() =>
            {
                attempts++;
                if (attempts <= 2) throw new InvalidOperationException("retry me");
                return "recovered";
            });

            Assert.Equal("recovered", result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public void Builder_RetryWithBackoff_Fixed_ExecutesSuccessfully()
        {
            int attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .RetryWithBackoff(2, TimeSpan.FromMilliseconds(10), BackoffType.Fixed);

            int result = policy.Execute(() =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("error");
                return 100;
            });

            Assert.Equal(100, result);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void Builder_RetryWithBackoff_Linear_ExecutesSuccessfully()
        {
            int attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .RetryWithBackoff(2, TimeSpan.FromMilliseconds(10), BackoffType.Linear);

            int result = policy.Execute(() =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("error");
                return 200;
            });

            Assert.Equal(200, result);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void Builder_RetryWithBackoff_ExponentialWithJitter_ExecutesSuccessfully()
        {
            int attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .RetryWithBackoff(2, TimeSpan.FromMilliseconds(5), BackoffType.Exponential, useJitter: true);

            int result = policy.Execute(() =>
            {
                attempts++;
                if (attempts == 1) throw new InvalidOperationException("error");
                return 300;
            });

            Assert.Equal(300, result);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void Builder_Timeout_ThrowsWhenExceeded()
        {
            var policy = VardPolicy.Handle<Exception>()
                .Timeout(TimeSpan.FromMilliseconds(50));

            Assert.Throws<TimeoutRejectedException>(() =>
            {
                policy.Execute(() =>
                {
                    Thread.Sleep(200);
                    return "slow";
                });
            });
        }

        [Fact]
        public void Builder_Fallback_Value_ReturnsFallbackOnException()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback("fallback-val");

            string result = policy.Execute<string>(() => throw new InvalidOperationException("failed"));

            Assert.Equal("fallback-val", result);
        }

        [Fact]
        public void Builder_Fallback_Func_ReturnsFallbackOnException()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() => 42);

            int result = policy.Execute<int>(() => throw new InvalidOperationException("failed"));

            Assert.Equal(42, result);
        }

        [Fact]
        public void Builder_Fallback_ContextualFunc_ReturnsFallbackOnException()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback((ex, ctx) => $"fallback-{ctx?["key"]}");

            var ctx = new Dictionary<string, object> { ["key"] = "test" };
            string result = policy.Execute<string>(_ => throw new InvalidOperationException("failed"), ctx);

            Assert.Equal("fallback-test", result);
        }

        [Fact]
        public void Builder_Fallback_VoidAction_ExecutesFallbackAction()
        {
            bool executed = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() => { executed = true; });

            policy.Execute(() => throw new InvalidOperationException("failed"));

            Assert.True(executed);
        }

        [Fact]
        public async Task Builder_FallbackAsync_ExecutesAsyncFallback()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .FallbackAsync(async (ex, ctx, ct) =>
                {
                    await Task.Yield();
                    return "async-val";
                });

            string result = await policy.ExecuteAsync<string>(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("failed");
            });

            Assert.Equal("async-val", result);
        }

        [Fact]
        public void VardPolicy_DirectStaticMethods_WorkAsExpected()
        {
            // VardPolicy.Retry
            var retryPolicy = VardPolicy.Retry(1);
            Assert.NotNull(retryPolicy);

            // VardPolicy.RetryWithBackoff
            var backoffPolicy = VardPolicy.RetryWithBackoff(1, TimeSpan.FromMilliseconds(10));
            Assert.NotNull(backoffPolicy);

            // VardPolicy.Timeout
            var timeoutPolicy = VardPolicy.Timeout(TimeSpan.FromSeconds(1));
            Assert.NotNull(timeoutPolicy);

            // VardPolicy.Fallback (value)
            var fallbackPolicy = VardPolicy.Fallback("default");
            Assert.NotNull(fallbackPolicy);
            Assert.Equal("default", fallbackPolicy.Execute<string>(() => throw new Exception("fail")));

            // VardPolicy.Fallback (void)
            bool fallbackVoidRan = false;
            var voidFallbackPolicy = VardPolicy.Fallback(() => fallbackVoidRan = true);
            Assert.NotNull(voidFallbackPolicy);
            voidFallbackPolicy.Execute(() => throw new Exception("fail"));
            Assert.True(fallbackVoidRan);
        }
    }
}
