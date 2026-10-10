using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Vard.Abstractions;
using Vard.Builders;
using Xunit;

namespace Vard.Tests
{
    public class HedgingPolicyTests
    {
        // 1. Primary_CompletesBeforeDelay_ReturnsPrimaryResult_NoHedgesSpawned
        [Fact]
        public async Task Primary_CompletesBeforeDelay_ReturnsPrimaryResult_NoHedgesSpawned()
        {
            var attempts = 0;
            var policy = VardPolicy.Handle<Exception>()
                .Hedging(maxHedges: 2, hedgingDelay: TimeSpan.FromMilliseconds(200));

            var result = await policy.ExecuteAsync(async ct =>
            {
                Interlocked.Increment(ref attempts);
                await Task.Delay(20, ct);
                return "primary_success";
            });

            result.Should().Be("primary_success");
            attempts.Should().Be(1);
        }

        // 2. Primary_Slow_HedgeWins_ReturnsHedgeResult
        [Fact]
        public async Task Primary_Slow_HedgeWins_ReturnsHedgeResult()
        {
            var attempts = 0;
            var policy = VardPolicy.Handle<Exception>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(40));

            var result = await policy.ExecuteAsync(async ct =>
            {
                var currentAttempt = Interlocked.Increment(ref attempts);
                if (currentAttempt == 1)
                {
                    await Task.Delay(300, ct);
                    return "primary_slow";
                }

                await Task.Delay(10, ct);
                return "hedge_fast";
            });

            result.Should().Be("hedge_fast");
            attempts.Should().Be(2);
        }

        // 3. Winner_CancelsLosingHedgesImmediately
        [Fact]
        public async Task Winner_CancelsLosingHedgesImmediately()
        {
            var loserCancelled = false;
            var policy = VardPolicy.Handle<Exception>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(30));

            var result = await policy.ExecuteAsync(async ct =>
            {
                if (!ct.CanBeCanceled) throw new InvalidOperationException("Token must be cancelable");

                try
                {
                    await Task.Delay(300, ct);
                    return "slow";
                }
                catch (OperationCanceledException)
                {
                    loserCancelled = true;
                    throw;
                }
            });

            // The winner completes fast in attempt 2
            // Let's configure a policy where attempt 2 is fast
            var attempts = 0;
            loserCancelled = false;
            var tcsLoserCancelled = new TaskCompletionSource<bool>();

            var hedging = VardPolicy.Handle<Exception>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(30));

            var winResult = await hedging.ExecuteAsync(async ct =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    try
                    {
                        await Task.Delay(400, ct);
                        return "slow_1";
                    }
                    catch (OperationCanceledException)
                    {
                        loserCancelled = true;
                        tcsLoserCancelled.TrySetResult(true);
                        throw;
                    }
                }

                await Task.Delay(10, ct);
                return "fast_2";
            });

            winResult.Should().Be("fast_2");
            var cancelledInTime = await Task.WhenAny(tcsLoserCancelled.Task, Task.Delay(500));
            cancelledInTime.Should().Be(tcsLoserCancelled.Task);
            loserCancelled.Should().BeTrue();
        }

        // 4. EarlyFailure_BeforeDelay_LaunchesNextHedgeImmediately
        [Fact]
        public async Task EarlyFailure_BeforeDelay_LaunchesNextHedgeImmediately()
        {
            var attempts = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(500));

            var result = await policy.ExecuteAsync(async ct =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    await Task.Delay(10, ct);
                    throw new InvalidOperationException("early failure");
                }

                await Task.Delay(10, ct);
                return "hedge_recovered";
            });

            sw.Stop();
            result.Should().Be("hedge_recovered");
            attempts.Should().Be(2);
            sw.ElapsedMilliseconds.Should().BeLessThan(350); // Much less than the 500ms delay!
        }

        // 5. HandleResult_TreatsMatchingResultAsFailure_LaunchesHedge
        [Fact]
        public async Task HandleResult_TreatsMatchingResultAsFailure_LaunchesHedge()
        {
            var attempts = 0;
            var policy = VardPolicy.HandleResult<int>(res => res == 500)
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(30));

            var result = await policy.ExecuteAsync(async ct =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    await Task.Delay(10, ct);
                    return 500;
                }

                await Task.Delay(10, ct);
                return 200;
            });

            result.Should().Be(200);
            attempts.Should().Be(2);
        }

        // 6. UnhandledException_AbortsHedgingImmediately
        [Fact]
        public async Task UnhandledException_AbortsHedgingImmediately()
        {
            var attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Hedging(maxHedges: 2, hedgingDelay: TimeSpan.FromMilliseconds(50));

            Func<Task> act = async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    Interlocked.Increment(ref attempts);
                    await Task.Delay(10, ct);
                    throw new ArgumentException("unhandled exception");
                });
            };

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("unhandled exception");
            attempts.Should().Be(1);
        }

        // 7. AllAttemptsFail_RethrowsLastException
        [Fact]
        public async Task AllAttemptsFail_RethrowsLastException()
        {
            var attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Hedging(maxHedges: 2, hedgingDelay: TimeSpan.FromMilliseconds(20));

            Func<Task> act = async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    var att = Interlocked.Increment(ref attempts);
                    await Task.Delay(10, ct);
                    throw new InvalidOperationException($"failure_{att}");
                });
            };

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure_3");
            attempts.Should().Be(3); // 1 initial + 2 hedges
        }

        // 8. AllAttemptsHandledResult_Exhausts_ReturnsLastResultOrFailure
        [Fact]
        public async Task AllAttemptsHandledResult_Exhausts_ReturnsLastResultOrFailure()
        {
            var attempts = 0;
            var policy = VardPolicy.HandleResult<string>(s => s == "bad")
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(20));

            var executeResult = await policy.ExecuteAsync(async ct =>
            {
                Interlocked.Increment(ref attempts);
                await Task.Delay(10, ct);
                return "bad";
            });

            executeResult.Should().Be("bad");

            var captureResult = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return "bad";
            });

            captureResult.IsSuccess.Should().BeFalse();
            captureResult.ExceptionType.Should().Be(ExceptionType.HandledByResult);
            captureResult.Exception.Should().NotBeNull();
            captureResult.Exception!.Message.Should().Contain("bad");
        }

        // 9. DynamicDelayProvider_CalculatesProgressiveDelayPerAttempt
        [Fact]
        public async Task DynamicDelayProvider_CalculatesProgressiveDelayPerAttempt()
        {
            var recordedAttempts = new List<int>();
            var policy = VardPolicy.Handle<Exception>()
                .Hedging(
                    maxHedges: 2,
                    hedgingDelayProvider: attempt =>
                    {
                        lock (recordedAttempts) { recordedAttempts.Add(attempt); }
                        return TimeSpan.FromMilliseconds(attempt * 20);
                    });

            var executeAttempts = 0;
            var result = await policy.ExecuteAsync(async ct =>
            {
                var att = Interlocked.Increment(ref executeAttempts);
                if (att < 3)
                {
                    await Task.Delay(100, ct);
                    return $"slow_{att}";
                }
                return "winner_3";
            });

            result.Should().Be("winner_3");
            executeAttempts.Should().Be(3);
            recordedAttempts.Should().ContainInOrder(1, 2);
        }

        // 10. Validation_MaxHedgesLessThanOne_ThrowsArgumentOutOfRangeException
        [Fact]
        public void Validation_MaxHedgesLessThanOne_ThrowsArgumentOutOfRangeException()
        {
            Action act = () => VardPolicy.Handle<Exception>().Hedging(maxHedges: 0);
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxHedges");

            Action actNeg = () => VardPolicy.Handle<Exception>().Hedging(maxHedges: -1);
            actNeg.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxHedges");
        }

        // 11. ExternalCancellation_PropagatesOperationCanceledException
        [Fact]
        public async Task ExternalCancellation_PropagatesOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var policy = VardPolicy.Handle<Exception>().Hedging(maxHedges: 2);

            Func<Task> act = async () =>
            {
                await policy.ExecuteAsync(async ct =>
                {
                    await Task.Delay(100, ct);
                    return "should_not_run";
                }, cancellationToken: cts.Token);
            };

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        // 12. Observability_OnHedgingResult_SyncAndAsync_ReceiveCorrectMetadata
        [Fact]
        public async Task Observability_OnHedgingResult_SyncAndAsync_ReceiveCorrectMetadata()
        {
            object? recordedResult = null;
            var recordedAttempt = 0;
            var recordedDuration = TimeSpan.Zero;
            IDictionary<string, object>? recordedContext = null;
            var asyncCallbackInvoked = false;

            var policy = VardPolicy.Handle<Exception>()
                .Hedging(
                    maxHedges: 1,
                    hedgingDelay: TimeSpan.FromMilliseconds(30),
                    onHedgingResult: (res, attempt, duration, ctx) =>
                    {
                        recordedResult = res;
                        recordedAttempt = attempt;
                        recordedDuration = duration;
                        recordedContext = ctx;
                    },
                    onHedgingResultAsync: async (res, attempt, duration, ctx) =>
                    {
                        await Task.Yield();
                        asyncCallbackInvoked = true;
                    });

            var context = new Dictionary<string, object> { ["correlationId"] = "xyz-123" };
            var attempts = 0;

            var result = await policy.ExecuteAsync(async (ctx, ct) =>
            {
                var att = Interlocked.Increment(ref attempts);
                if (att == 1)
                {
                    await Task.Delay(100, ct);
                    return "slow";
                }
                return "fast_winner";
            }, context);

            result.Should().Be("fast_winner");
            recordedResult.Should().Be("fast_winner");
            recordedAttempt.Should().Be(2);
            recordedDuration.Should().BeGreaterThan(TimeSpan.Zero);
            recordedContext.Should().NotBeNull();
            recordedContext!["correlationId"].Should().Be("xyz-123");
            asyncCallbackInvoked.Should().BeTrue();

            // Fail-fast test: if callback throws, exception is propagated directly
            var failingPolicy = VardPolicy.Handle<Exception>()
                .Hedging(
                    maxHedges: 1,
                    onHedgingResult: (_, _, _, _) => throw new InvalidOperationException("callback_failure"));

            Func<Task> failingAct = async () => await failingPolicy.ExecuteAsync(_ => Task.FromResult("ok"));
            await failingAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("callback_failure");
        }

        // 13. VoidAction_Support_SucceedsViaPolicyActionExtensions
        [Fact]
        public async Task VoidAction_Support_SucceedsViaPolicyActionExtensions()
        {
            var executed = false;
            var policy = VardPolicy.Handle<Exception>().Hedging(maxHedges: 1);

            await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                executed = true;
            });

            executed.Should().BeTrue();

            var syncExecuted = false;
            policy.Execute(() =>
            {
                syncExecuted = true;
            });

            syncExecuted.Should().BeTrue();
        }

        // 14. SynchronousExecution_UnwrapsCleanlyAndExecutesConcurrently
        [Fact]
        public void SynchronousExecution_UnwrapsCleanlyAndExecutesConcurrently()
        {
            var attempts = 0;
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(30));

            var result = policy.Execute(() =>
            {
                var att = Interlocked.Increment(ref attempts);
                if (att == 1)
                {
                    Thread.Sleep(150);
                    return "slow_1";
                }

                Thread.Sleep(10);
                return "fast_2";
            });

            result.Should().Be("fast_2");

            // Verify clean exception unwrapping (preserves original type without AggregateException)
            var failingPolicy = VardPolicy.Handle<ArgumentException>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(10));

            Action act = () => failingPolicy.Execute<string>(() => throw new ArgumentException("clean_exception"));
            act.Should().Throw<ArgumentException>().WithMessage("clean_exception");
        }

        // 15. ExecuteAndCapture_SuccessAndFailure
        [Fact]
        public async Task ExecuteAndCapture_SuccessAndFailure()
        {
            var policy = VardPolicy.Handle<InvalidOperationException>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(20));

            var successResult = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return "success_val";
            });

            successResult.IsSuccess.Should().BeTrue();
            successResult.Result.Should().Be("success_val");
            successResult.Exception.Should().BeNull();

            var failResult = await policy.ExecuteAndCaptureAsync<string>(async ct =>
            {
                await Task.Delay(10, ct);
                throw new InvalidOperationException("hedging_exhausted");
            });

            failResult.IsSuccess.Should().BeFalse();
            failResult.Exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("hedging_exhausted");
            failResult.ExceptionType.Should().Be(ExceptionType.HandledByCondition);
        }
    }
}
