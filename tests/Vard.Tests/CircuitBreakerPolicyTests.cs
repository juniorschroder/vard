using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;
using Xunit;

namespace Vard.Tests
{
    public class CircuitBreakerPolicyTests
    {
        [Fact]
        public void Constructor_InvalidParameters_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.CircuitBreaker(0, TimeSpan.FromSeconds(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.CircuitBreaker(1, TimeSpan.Zero));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.CircuitBreaker(1, TimeSpan.FromMilliseconds(-1)));
        }

        [Fact]
        public void Execute_SuccessfulCalls_StaysClosed()
        {
            var policy = VardPolicy.CircuitBreaker(2, TimeSpan.FromMilliseconds(200));

            int result = policy.Execute(() => 42);

            Assert.Equal(42, result);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public void Execute_FailuresBelowThreshold_StaysClosed()
        {
            var policy = VardPolicy.CircuitBreaker(2, TimeSpan.FromMilliseconds(200));

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("1")));

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Execute_SuccessResetsConsecutiveFailures()
        {
            var policy = VardPolicy.CircuitBreaker(2, TimeSpan.FromMilliseconds(200));

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("fail 1")));

            // Succeeded call resets consecutive count
            int result = policy.Execute(() => 100);
            Assert.Equal(100, result);

            // Another single failure should not trip the breaker since count was reset
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("fail 2")));

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Execute_ConsecutiveFailuresReachThreshold_TripsToOpen()
        {
            var onBreakFired = false;
            Exception? brokenEx = null;
            TimeSpan brokenDuration = TimeSpan.Zero;

            var policy = VardPolicy.Handle<InvalidOperationException>()
                .CircuitBreaker(
                    2,
                    TimeSpan.FromMilliseconds(300),
                    onBreak: (ex, duration, _) =>
                    {
                        onBreakFired = true;
                        brokenEx = ex;
                        brokenDuration = duration;
                    });

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("first failure")));

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.False(onBreakFired);

            // Second consecutive failure trips the circuit
            var ex2 = Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("second failure")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
            Assert.True(onBreakFired);
            Assert.Same(ex2, brokenEx);
            Assert.Equal(TimeSpan.FromMilliseconds(300), brokenDuration);
            Assert.Same(ex2, policy.LastException);
        }

        [Fact]
        public void Execute_WhenOpen_RejectsCallsImmediately()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("boom")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            var executed = false;
            var openEx = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() =>
                {
                    executed = true;
                    return 1;
                }));

            Assert.False(executed);
            Assert.Equal(CircuitState.Open, openEx.State);
            Assert.NotNull(openEx.RetryAfter);
            Assert.True(openEx.RetryAfter > TimeSpan.Zero);
            Assert.NotNull(openEx.InnerException);
            Assert.Same(policy.LastException, openEx.InnerException);
        }

        [Fact]
        public void Execute_UnhandledException_DoesNotTripCircuit()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .CircuitBreaker(1, TimeSpan.FromSeconds(1));

            // ArgumentException is not handled by this policy
            Assert.Throws<ArgumentException>(() =>
                policy.Execute(() => throw new ArgumentException("not handled")));

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public async Task Execute_AfterBreakDuration_TransitionsToHalfOpenAndExecutesPilot()
        {
            var onHalfOpenFired = false;
            var onResetFired = false;

            var policy = VardPolicy.CircuitBreaker(
                1,
                TimeSpan.FromMilliseconds(60),
                onHalfOpen: _ => onHalfOpenFired = true,
                onReset: _ => onResetFired = true);

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            // First call after duration should run as HalfOpen pilot and succeed
            int result = policy.Execute(() => 999);

            Assert.Equal(999, result);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.True(onHalfOpenFired);
            Assert.True(onResetFired);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public async Task Execute_HalfOpen_FailedPilot_ReturnsToOpen()
        {
            int breakCount = 0;
            var policy = VardPolicy.CircuitBreaker(
                1,
                TimeSpan.FromMilliseconds(60),
                onBreak: (_, _, _) => breakCount++);

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 1")));

            Assert.Equal(1, breakCount);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            // Pilot call fails
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 2")));

            Assert.Equal(2, breakCount);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            // Next call should immediately fail-fast as Open
            var cbEx = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => 1));

            Assert.Equal(CircuitState.Open, cbEx.State);
        }

        [Fact]
        public void Isolate_PutsCircuitInIsolatedState_RejectsCallsUntilReset()
        {
            var policy = VardPolicy.CircuitBreaker(2, TimeSpan.FromSeconds(10));

            policy.Isolate();

            Assert.Equal(CircuitState.Isolated, policy.CircuitState);

            var ex = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => 123));

            Assert.Equal(CircuitState.Isolated, ex.State);
            Assert.Null(ex.RetryAfter);

            // Reset restores to closed
            policy.Reset();
            Assert.Equal(CircuitState.Closed, policy.CircuitState);

            int val = policy.Execute(() => 456);
            Assert.Equal(456, val);
        }

        [Fact]
        public void Reset_ClearsFailuresAndState()
        {
            var onResetFired = false;
            var policy = VardPolicy.CircuitBreaker(
                2,
                TimeSpan.FromSeconds(10),
                onReset: _ => onResetFired = true);

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err")));

            Assert.NotNull(policy.LastException);

            policy.Reset();

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.Null(policy.LastException);
            Assert.True(onResetFired);
        }

        [Fact]
        public async Task ExecuteAsync_TransitionsAndAsyncCallbacksWork()
        {
            bool breakAsyncCalled = false;
            bool resetAsyncCalled = false;
            bool halfOpenAsyncCalled = false;

            var policy = VardPolicy.CircuitBreaker(
                1,
                TimeSpan.FromMilliseconds(60),
                onBreakAsync: (ex, ts, ctx) =>
                {
                    breakAsyncCalled = true;
                    return Task.CompletedTask;
                },
                onResetAsync: ctx =>
                {
                    resetAsyncCalled = true;
                    return Task.CompletedTask;
                },
                onHalfOpenAsync: ctx =>
                {
                    halfOpenAsyncCalled = true;
                    return Task.CompletedTask;
                });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                policy.ExecuteAsync<int>(_ => throw new InvalidOperationException("fail")));

            Assert.True(breakAsyncCalled);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            int result = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 777;
            });

            Assert.Equal(777, result);
            Assert.True(halfOpenAsyncCalled);
            Assert.True(resetAsyncCalled);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Callback_ThrowsException_PropagatesFailFast()
        {
            var policy = VardPolicy.CircuitBreaker(
                1,
                TimeSpan.FromSeconds(5),
                onBreak: (_, _, _) => throw new FormatException("Callback failure!"));

            var ex = Assert.Throws<FormatException>(() =>
                policy.Execute(() => throw new InvalidOperationException("trip")));

            Assert.Equal("Callback failure!", ex.Message);
        }

        [Fact]
        public void ExecuteAndCapture_OpenCircuit_CapturesException()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromSeconds(5));

            policy.ExecuteAndCapture<int>(() => throw new InvalidOperationException("fail"));

            var capture = policy.ExecuteAndCapture<string>(() => "ok");

            Assert.False(capture.IsSuccess);
            Assert.NotNull(capture.FinalException);
            Assert.IsType<CircuitBreakerOpenException>(capture.FinalException);
            var cbEx = (CircuitBreakerOpenException)capture.FinalException!;
            Assert.Equal(CircuitState.Open, cbEx.State);
        }

        [Fact]
        public async Task ExecuteAndCaptureAsync_OpenCircuit_CapturesException()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromSeconds(5));

            await policy.ExecuteAndCaptureAsync<int>(async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("fail");
            });

            var capture = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Yield();
                return "hello";
            });

            Assert.False(capture.IsSuccess);
            Assert.IsType<CircuitBreakerOpenException>(capture.FinalException);
        }

        [Fact]
        public void Context_IsPropagatedToCallbacks()
        {
            IDictionary<string, object>? capturedContext = null;

            var policy = VardPolicy.CircuitBreaker(
                1,
                TimeSpan.FromSeconds(5),
                onBreak: (ex, ts, ctx) => capturedContext = ctx);

            var context = new Dictionary<string, object> { ["CorrelationId"] = "xyz-123" };

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(_ => throw new InvalidOperationException("boom"), context));

            Assert.NotNull(capturedContext);
            Assert.Equal("xyz-123", capturedContext!["CorrelationId"]);
        }

        [Fact]
        public async Task Execute_HalfOpen_PilotRunning_RejectsOtherCallsWithHalfOpenState()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromMilliseconds(50));

            // Trip breaker
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("trip")));

            await Task.Delay(70);

            var pilotStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pilotContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var pilotTask = Task.Run(() =>
            {
                return policy.Execute(() =>
                {
                    pilotStarted.SetResult(true);
                    pilotContinue.Task.GetAwaiter().GetResult();
                    return "pilot-done";
                });
            });

            await pilotStarted.Task;

            // At this point, the pilot is running inside HalfOpen!
            // Any other call should be rejected immediately with HalfOpen state
            var ex = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => "concurrent-call"));

            Assert.Equal(CircuitState.HalfOpen, ex.State);
            Assert.Equal(TimeSpan.Zero, ex.RetryAfter);

            pilotContinue.SetResult(true);
            var pilotResult = await pilotTask;

            Assert.Equal("pilot-done", pilotResult);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Execute_VoidAction_SucceedsAndFailsCorrectly()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromSeconds(5));

            int counter = 0;
            policy.Execute(() => { counter++; });
            Assert.Equal(1, counter);

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("void fail")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
        }

        [Fact]
        public async Task ExecuteAsync_VoidAction_SucceedsAndFailsCorrectly()
        {
            var policy = VardPolicy.CircuitBreaker(1, TimeSpan.FromSeconds(5));

            int counter = 0;
            await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                counter++;
            });
            Assert.Equal(1, counter);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                policy.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("async void fail");
                }));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
        }
    }
}
