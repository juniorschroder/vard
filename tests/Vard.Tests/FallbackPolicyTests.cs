using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class FallbackPolicyTests
    {
        [Fact]
        public void Execute_HandledException_ReturnsStaticFallbackValue()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback("fallback-default");

            string result = policy.Execute<string>(() => throw new InvalidOperationException("failed"));

            Assert.Equal("fallback-default", result);
        }

        [Fact]
        public void Execute_HandledException_InvokesFallbackDelegate()
        {
            int invocations = 0;
            var policy = VardPolicy.Handle<HttpRequestException>()
                .Fallback(() =>
                {
                    invocations++;
                    return 999;
                });

            int result = policy.Execute<int>(() => throw new HttpRequestException("network down"));

            Assert.Equal(999, result);
            Assert.Equal(1, invocations);
        }

        [Fact]
        public void Execute_HandledException_InvokesContextualFallbackDelegate()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback((ex, ctx) =>
                {
                    Assert.NotNull(ex);
                    Assert.Equal("boom", ex!.Message);
                    Assert.Equal("order-123", ctx?["OrderId"]);
                    return "recovered";
                });

            var context = new Dictionary<string, object> { ["OrderId"] = "order-123" };
            string result = policy.Execute<string>(_ => throw new InvalidOperationException("boom"), context);

            Assert.Equal("recovered", result);
        }

        [Fact]
        public void Execute_UnhandledException_PropagatesExceptionWithoutFallback()
        {
            bool fallbackInvoked = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() =>
                {
                    fallbackInvoked = true;
                    return "fallback";
                });

            Assert.Throws<ArgumentException>(() =>
            {
                policy.Execute<string>(() => throw new ArgumentException("wrong arg"));
            });

            Assert.False(fallbackInvoked);
        }

        [Fact]
        public void Execute_HandledResult_TriggersFallback()
        {
            var policy = VardPolicy.HandleResult<int>(r => r == -1)
                .Fallback(0);

            int result = policy.Execute(() => -1);

            Assert.Equal(0, result);
        }

        [Fact]
        public void Execute_SuccessResult_DoesNotTriggerFallback()
        {
            var policy = VardPolicy.Handle<Exception>()
                .Fallback("fallback");

            string result = policy.Execute(() => "original-success");

            Assert.Equal("original-success", result);
        }

        [Fact]
        public async Task ExecuteAsync_HandledException_ReturnsAsyncFallbackValue()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .FallbackAsync(async (ex, ctx, ct) =>
                {
                    await Task.Yield();
                    return "async-fallback";
                });

            string result = await policy.ExecuteAsync<string>(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async fail");
            });

            Assert.Equal("async-fallback", result);
        }

        [Fact]
        public async Task ExecuteAsync_HandledResult_TriggersAsyncFallback()
        {
            var policy = VardPolicy.HandleResult<int>(r => r == 500)
                .FallbackAsync(async (ex, ctx, ct) =>
                {
                    await Task.Yield();
                    return 200;
                });

            int result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 500;
            });

            Assert.Equal(200, result);
        }

        [Fact]
        public void Execute_OnFallbackCallbacks_InvokedWithExceptionAndContext()
        {
            bool callbackInvoked = false;
            Exception? capturedEx = null;
            IDictionary<string, object>? capturedCtx = null;

            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(
                    fallbackValue: "safe",
                    onFallback: (ex, ctx) =>
                    {
                        callbackInvoked = true;
                        capturedEx = ex;
                        capturedCtx = ctx;
                    });

            var context = new Dictionary<string, object> { ["UserId"] = 42 };
            string result = policy.Execute<string>(_ => throw new InvalidOperationException("error"), context);

            Assert.Equal("safe", result);
            Assert.True(callbackInvoked);
            Assert.IsType<InvalidOperationException>(capturedEx);
            Assert.Same(context, capturedCtx);
        }

        [Fact]
        public async Task ExecuteAsync_OnFallbackAsyncCallback_Invoked()
        {
            bool callbackInvoked = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .FallbackAsync(
                    fallbackActionAsync: async (ex, ctx, ct) =>
                    {
                        await Task.Yield();
                        return "recovered";
                    },
                    onFallbackAsync: async (ex, ctx) =>
                    {
                        await Task.Yield();
                        callbackInvoked = true;
                    });

            string result = await policy.ExecuteAsync<string>(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("error");
            });

            Assert.Equal("recovered", result);
            Assert.True(callbackInvoked);
        }

        [Fact]
        public void ExecuteAndCapture_HandledException_ReturnsSuccessWithFallbackValue()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback("captured-fallback");

            var result = policy.ExecuteAndCapture<string>(() => throw new InvalidOperationException("error"));

            Assert.True(result.IsSuccess);
            Assert.Equal("captured-fallback", result.Result);
            Assert.Null(result.FinalException);
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_HandledException_ReturnsSuccessWithFallbackValue()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback("async-captured-fallback");

            var result = await policy.ExecuteAndCaptureAsync<string>(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("error");
            });

            Assert.True(result.IsSuccess);
            Assert.Equal("async-captured-fallback", result.Result);
            Assert.Null(result.FinalException);
        }

        // Void Fallback tests (FB-03)
        [Fact]
        public void VoidFallback_HandledException_ExecutesVoidFallbackAction()
        {
            bool fallbackExecuted = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() =>
                {
                    fallbackExecuted = true;
                });

            policy.Execute(() => throw new InvalidOperationException("fail"));

            Assert.True(fallbackExecuted);
        }

        [Fact]
        public void VoidFallback_UnhandledException_PropagatesException()
        {
            bool fallbackExecuted = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() =>
                {
                    fallbackExecuted = true;
                });

            Assert.Throws<ArgumentException>(() =>
            {
                policy.Execute(() => throw new ArgumentException("unhandled"));
            });

            Assert.False(fallbackExecuted);
        }

        [Fact]
        public async Task VoidFallbackAsync_HandledException_ExecutesAsyncVoidFallbackAction()
        {
            bool fallbackExecuted = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .FallbackAsync(async (ex, ctx, ct) =>
                {
                    await Task.Yield();
                    fallbackExecuted = true;
                });

            await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async void fail");
            });

            Assert.True(fallbackExecuted);
        }

        [Fact]
        public void VoidFallback_ExecuteAndCapture_HandledException_ReturnsSuccess()
        {
            bool fallbackExecuted = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Fallback(() =>
                {
                    fallbackExecuted = true;
                });

            var result = policy.ExecuteAndCapture(() => throw new InvalidOperationException("void fail"));

            Assert.True(result.IsSuccess);
            Assert.True(fallbackExecuted);
        }

        [Fact]
        public async Task VoidFallback_ExecuteAndCaptureAsync_HandledException_ReturnsSuccess()
        {
            bool fallbackExecuted = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .FallbackAsync(async (ex, ctx, ct) =>
                {
                    await Task.Yield();
                    fallbackExecuted = true;
                });

            var result = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async void fail");
            });

            Assert.True(result.IsSuccess);
            Assert.True(fallbackExecuted);
        }
    }
}
