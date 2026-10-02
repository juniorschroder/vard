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
    public class TimeoutPolicyTests
    {
        [Fact]
        public void Constructor_ZeroOrNegativeTimeout_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutPolicy(TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimeoutPolicy(TimeSpan.FromMilliseconds(-100)));
        }

        [Fact]
        public void Execute_WithinTimeout_ReturnsResultSuccessfully()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromSeconds(2));

            string result = policy.Execute(() => "success");

            Assert.Equal("success", result);
        }

        [Fact]
        public void Execute_ExceedsTimeout_ThrowsTimeoutRejectedException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromMilliseconds(50));

            var ex = Assert.Throws<TimeoutRejectedException>(() =>
            {
                policy.Execute(() =>
                {
                    Thread.Sleep(200);
                    return 42;
                });
            });

            Assert.Equal(TimeSpan.FromMilliseconds(50), ex.Timeout);
        }

        [Fact]
        public void Execute_ExceedsTimeout_InvokesOnTimeoutCallback()
        {
            bool callbackInvoked = false;
            TimeSpan capturedTimeout = TimeSpan.Zero;
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.Timeout(
                TimeSpan.FromMilliseconds(50),
                onTimeout: (ctx, timeout) =>
                {
                    callbackInvoked = true;
                    capturedTimeout = timeout;
                    capturedContext = ctx;
                });

            var context = new Dictionary<string, object> { ["traceId"] = "123" };

            Assert.Throws<TimeoutRejectedException>(() =>
            {
                policy.Execute(_ =>
                {
                    Thread.Sleep(200);
                    return 42;
                }, context);
            });

            Assert.True(callbackInvoked);
            Assert.Equal(TimeSpan.FromMilliseconds(50), capturedTimeout);
            Assert.Same(context, capturedContext);
        }

        [Fact]
        public void Execute_ActionThrowsException_PropagatesOriginalException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromSeconds(1));

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                policy.Execute<int>(() => throw new InvalidOperationException("boom"));
            });

            Assert.Equal("boom", ex.Message);
        }

        [Fact]
        public void ExecuteAndCapture_ExceedsTimeout_ReturnsFailureWithTimeoutRejectedException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromMilliseconds(50));

            var result = policy.ExecuteAndCapture(() =>
            {
                Thread.Sleep(200);
                return "completed";
            });

            Assert.False(result.IsSuccess);
            Assert.IsType<TimeoutRejectedException>(result.FinalException);
            Assert.Equal(ExceptionType.HandledByCondition, result.ExceptionType);
            Assert.Null(result.Result);
        }

        [Fact]
        public async Task ExecuteAsync_WithinTimeout_ReturnsResultSuccessfully()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromSeconds(2));

            string result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return "async success";
            });

            Assert.Equal("async success", result);
        }

        [Fact]
        public async Task ExecuteAsync_ExceedsTimeout_ThrowsTimeoutRejectedException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromMilliseconds(50));

            var ex = await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(500, ct);
                    return 42;
                });
            });

            Assert.Equal(TimeSpan.FromMilliseconds(50), ex.Timeout);
        }

        [Fact]
        public async Task ExecuteAsync_ExceedsTimeout_InvokesSyncOnTimeoutCallback()
        {
            bool callbackInvoked = false;
            var policy = VardPolicy.Timeout(
                TimeSpan.FromMilliseconds(50),
                onTimeout: (ctx, timeout) => callbackInvoked = true);

            await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(500, ct);
                    return 1;
                });
            });

            Assert.True(callbackInvoked);
        }

        [Fact]
        public async Task ExecuteAsync_ExceedsTimeout_InvokesAsyncOnTimeoutCallback()
        {
            bool callbackInvoked = false;
            var policy = VardPolicy.Timeout(
                TimeSpan.FromMilliseconds(50),
                onTimeoutAsync: async (ctx, timeout) =>
                {
                    await Task.Yield();
                    callbackInvoked = true;
                });

            await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(500, ct);
                    return 1;
                });
            });

            Assert.True(callbackInvoked);
        }

        [Fact]
        public async Task ExecuteAsync_CallerCancellation_ThrowsOperationCanceledExceptionWithoutOnTimeout()
        {
            // Decision D-05: Caller cancellation must propagate OperationCanceledException directly without invoking OnTimeout
            bool timeoutCallbackInvoked = false;
            var policy = VardPolicy.Timeout(
                TimeSpan.FromSeconds(5),
                onTimeout: (ctx, timeout) => timeoutCallbackInvoked = true);

            using var callerCts = new CancellationTokenSource();
            callerCts.Cancel(); // Pre-cancelled caller token

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(1000, ct);
                    return 1;
                }, callerCts.Token);
            });

            Assert.False(timeoutCallbackInvoked, "OnTimeout must NOT be invoked when cancellation originates from caller.");
        }

        [Fact]
        public async Task ExecuteAsync_ActionThrowsException_PropagatesOriginalException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromSeconds(1));

            var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await policy.ExecuteAsync<int>(async ct =>
                {
                    await Task.Yield();
                    throw new ArgumentException("invalid arg");
                });
            });

            Assert.Equal("invalid arg", ex.Message);
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_ExceedsTimeout_ReturnsFailureWithTimeoutRejectedException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromMilliseconds(50));

            var result = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Delay(500, ct);
                return "async completed";
            });

            Assert.False(result.IsSuccess);
            Assert.IsType<TimeoutRejectedException>(result.FinalException);
            Assert.Equal(ExceptionType.HandledByCondition, result.ExceptionType);
            Assert.Null(result.Result);
        }

        [Fact]
        public void Execute_VoidAction_WithinTimeout_Succeeds()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromSeconds(1));
            bool actionExecuted = false;

            policy.Execute(() => { actionExecuted = true; });

            Assert.True(actionExecuted);
        }

        [Fact]
        public async Task ExecuteAsync_VoidAction_ExceedsTimeout_ThrowsTimeoutRejectedException()
        {
            var policy = VardPolicy.Timeout(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(500, ct);
                });
            });
        }
    }
}
