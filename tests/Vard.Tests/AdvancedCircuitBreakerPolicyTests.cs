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
    public class AdvancedCircuitBreakerPolicyTests
    {
        [Fact]
        public void Constructor_InvalidParameters_ThrowsArgumentException()
        {
            // failureThreshold <= 0 or > 1
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(0.0, TimeSpan.FromSeconds(10), 5, TimeSpan.FromSeconds(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(1.1, TimeSpan.FromSeconds(10), 5, TimeSpan.FromSeconds(1)));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(-0.1, TimeSpan.FromSeconds(10), 5, TimeSpan.FromSeconds(1)));

            // samplingDuration <= 0
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(0.5, TimeSpan.Zero, 5, TimeSpan.FromSeconds(1)));

            // minimumThroughput <= 0
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(0.5, TimeSpan.FromSeconds(10), 0, TimeSpan.FromSeconds(1)));

            // durationOfBreak <= 0
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VardPolicy.AdvancedCircuitBreaker(0.5, TimeSpan.FromSeconds(10), 5, TimeSpan.Zero));
        }

        [Fact]
        public void Execute_SuccessfulCalls_StaysClosed()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(1));

            for (int i = 0; i < 10; i++)
            {
                int res = policy.Execute(() => i);
                Assert.Equal(i, res);
            }

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public void Execute_HighFailureRateButBelowThroughput_RemainsClosed()
        {
            // 4 failures out of 4 calls = 100% failure rate, but minimumThroughput = 5
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(1));

            for (int i = 0; i < 4; i++)
            {
                Assert.Throws<InvalidOperationException>(() =>
                    policy.Execute(() => throw new InvalidOperationException("err")));
            }

            // Breaker MUST remain closed because 4 < 5 minimum throughput!
            Assert.Equal(CircuitState.Closed, policy.CircuitState);

            // 5th call fails: now total throughput = 5, failures = 5, rate = 100% >= 50% -> trips!
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 5")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
        }

        [Fact]
        public void Execute_ThroughputReached_FailureRateBelowThreshold_RemainsClosed()
        {
            // 10 calls, 3 failures (30%), threshold = 50%, minimumThroughput = 5
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(1));

            for (int i = 0; i < 7; i++)
            {
                policy.Execute(() => i);
            }

            for (int i = 0; i < 3; i++)
            {
                Assert.Throws<InvalidOperationException>(() =>
                    policy.Execute(() => throw new InvalidOperationException("err")));
            }

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Execute_ThroughputReached_FailureRateMeetsThreshold_TripsToOpen()
        {
            var onBreakCalled = false;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .AdvancedCircuitBreaker(
                    failureThreshold: 0.5,
                    samplingDuration: TimeSpan.FromSeconds(10),
                    minimumThroughput: 4,
                    durationOfBreak: TimeSpan.FromMilliseconds(200),
                    onBreak: (ex, ts, ctx) => onBreakCalled = true);

            // 2 successes, 2 failures -> total 4, failures = 2 (50% >= 50%) -> trips
            policy.Execute(() => 1);
            policy.Execute(() => 2);
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 1")));
            Assert.Equal(CircuitState.Closed, policy.CircuitState);

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 2")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
            Assert.True(onBreakCalled);
            Assert.NotNull(policy.LastException);
        }

        [Fact]
        public void Execute_WhenOpen_RejectsImmediately()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromMilliseconds(500));

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 1")));
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 2")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            bool actionRan = false;
            var ex = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() =>
                {
                    actionRan = true;
                    return 10;
                }));

            Assert.False(actionRan);
            Assert.Equal(CircuitState.Open, ex.State);
            Assert.NotNull(ex.RetryAfter);
            Assert.True(ex.RetryAfter > TimeSpan.Zero);
            Assert.Same(policy.LastException, ex.InnerException);
        }

        [Fact]
        public async Task Execute_SamplingDurationExpiration_OldFailuresSlideOut()
        {
            // sampling duration = 100ms, minimum throughput = 2
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromMilliseconds(100),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromSeconds(1));

            // 1 failure
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 1")));

            // Wait 150ms for sampling window to slide out
            await Task.Delay(150);

            // 1 success
            policy.Execute(() => 1);

            // 1 failure - total in current window: 1 success, 1 failure (rate = 50%, throughput = 2) -> trips!
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("err 2")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);
        }

        [Fact]
        public async Task Execute_HalfOpen_PilotTrialSucceeds_ClosesBreaker()
        {
            var onResetCalled = false;
            var onHalfOpenCalled = false;

            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromMilliseconds(60),
                onReset: _ => onResetCalled = true,
                onHalfOpen: _ => onHalfOpenCalled = true);

            // Trip breaker
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("1")));
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("2")));

            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            // HalfOpen trial
            int res = policy.Execute(() => 42);

            Assert.Equal(42, res);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.True(onHalfOpenCalled);
            Assert.True(onResetCalled);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public async Task Execute_HalfOpen_PilotTrialFails_ReopensBreaker()
        {
            int breakCount = 0;
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromMilliseconds(60),
                onBreak: (_, _, _) => breakCount++);

            // Trip breaker
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("1")));
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("2")));

            Assert.Equal(1, breakCount);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            // HalfOpen pilot fails
            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(() => throw new InvalidOperationException("pilot fail")));

            Assert.Equal(2, breakCount);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            // Subsequent call is rejected
            var cbEx = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => 99));

            Assert.Equal(CircuitState.Open, cbEx.State);
        }

        [Fact]
        public async Task Execute_HalfOpen_ConcurrentCall_RejectedWithHalfOpenState()
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

            await Task.Delay(70);

            var pilotStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pilotContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var pilotTask = Task.Run(() =>
            {
                return policy.Execute(() =>
                {
                    pilotStarted.SetResult(true);
                    pilotContinue.Task.GetAwaiter().GetResult();
                    return "pilot-ok";
                });
            });

            await pilotStarted.Task;

            var ex = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => "concurrent"));

            Assert.Equal(CircuitState.HalfOpen, ex.State);
            Assert.Equal(TimeSpan.Zero, ex.RetryAfter);

            pilotContinue.SetResult(true);
            string pilotRes = await pilotTask;

            Assert.Equal("pilot-ok", pilotRes);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void Isolate_And_Reset_ManualControls()
        {
            var onResetCalled = false;
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromSeconds(10),
                onReset: _ => onResetCalled = true);

            policy.Isolate();
            Assert.Equal(CircuitState.Isolated, policy.CircuitState);

            var ex = Assert.Throws<CircuitBreakerOpenException>(() =>
                policy.Execute(() => "isolated"));

            Assert.Equal(CircuitState.Isolated, ex.State);
            Assert.Null(ex.RetryAfter);

            policy.Reset();
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.True(onResetCalled);
            Assert.Null(policy.LastException);

            int val = policy.Execute(() => 100);
            Assert.Equal(100, val);
        }

        [Fact]
        public void Execute_UnhandledException_DoesNotCountAsFailure()
        {
            // Only handle InvalidOperationException
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .AdvancedCircuitBreaker(
                    failureThreshold: 0.5,
                    samplingDuration: TimeSpan.FromSeconds(5),
                    minimumThroughput: 2,
                    durationOfBreak: TimeSpan.FromSeconds(10));

            // Throw ArgumentNullException - unhandled
            Assert.Throws<ArgumentNullException>(() =>
                policy.Execute(() => throw new ArgumentNullException("not handled")));

            Assert.Equal(CircuitState.Closed, policy.CircuitState);
            Assert.Null(policy.LastException);
        }

        [Fact]
        public async Task ExecuteAsync_AsyncCallbacksAndVoidAction()
        {
            bool breakAsyncCalled = false;
            bool resetAsyncCalled = false;
            bool halfOpenAsyncCalled = false;

            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 2,
                durationOfBreak: TimeSpan.FromMilliseconds(60),
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
                policy.ExecuteAsync<int>(_ => throw new InvalidOperationException("1")));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                policy.ExecuteAsync<int>(_ => throw new InvalidOperationException("2")));

            Assert.True(breakAsyncCalled);
            Assert.Equal(CircuitState.Open, policy.CircuitState);

            await Task.Delay(80);

            int res = await policy.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return 555;
            });

            Assert.Equal(555, res);
            Assert.True(halfOpenAsyncCalled);
            Assert.True(resetAsyncCalled);
            Assert.Equal(CircuitState.Closed, policy.CircuitState);
        }

        [Fact]
        public void CallbackException_PropagatesFailFast()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 1,
                durationOfBreak: TimeSpan.FromSeconds(10),
                onBreak: (_, _, _) => throw new NotSupportedException("callback crash"));

            var ex = Assert.Throws<NotSupportedException>(() =>
                policy.Execute(() => throw new InvalidOperationException("trigger")));

            Assert.Equal("callback crash", ex.Message);
        }

        [Fact]
        public void ExecuteAndCapture_OpenBreaker_CapturesException()
        {
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 1,
                durationOfBreak: TimeSpan.FromSeconds(10));

            policy.ExecuteAndCapture<int>(() => throw new InvalidOperationException("trip"));

            var capture = policy.ExecuteAndCapture<string>(() => "should-not-run");

            Assert.False(capture.IsSuccess);
            Assert.IsType<CircuitBreakerOpenException>(capture.FinalException);
        }

        [Fact]
        public void Context_PropagatedToCallbacks()
        {
            IDictionary<string, object>? receivedContext = null;
            var policy = VardPolicy.AdvancedCircuitBreaker(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 1,
                durationOfBreak: TimeSpan.FromSeconds(10),
                onBreak: (ex, ts, ctx) => receivedContext = ctx);

            var context = new Dictionary<string, object> { ["TraceId"] = "trace-456" };

            Assert.Throws<InvalidOperationException>(() =>
                policy.Execute(_ => throw new InvalidOperationException("fail"), context));

            Assert.NotNull(receivedContext);
            Assert.Equal("trace-456", receivedContext!["TraceId"]);
        }
    }
}
