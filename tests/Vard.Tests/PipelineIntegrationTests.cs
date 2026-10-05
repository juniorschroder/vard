using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Vard.Abstractions;
using Vard.Builders;
using Vard.Policies;
using Xunit;

namespace Vard.Tests
{
    public class PipelineIntegrationTests
    {
        // =========================================================================
        // Parte 1: Pipeline Corporativo (Fallback -> Retry -> CircuitBreaker -> Timeout -> Bulkhead)
        // =========================================================================

        [Fact]
        public async Task CorporatePipeline_Success_PassesThroughAllLayers()
        {
            var fallback = VardPolicy.Handle<Exception>().Fallback<string>("fallback_value");
            var retry = VardPolicy.Handle<HttpRequestException>().Retry(2);
            var cb = VardPolicy.Handle<HttpRequestException>().CircuitBreaker(3, TimeSpan.FromSeconds(5));
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(2));
            var bulkhead = VardPolicy.Bulkhead(5, 5);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            var result = await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return "corporate_success";
            });

            result.Should().Be("corporate_success");
        }

        [Fact]
        public async Task CorporatePipeline_TransientError_RetryRecovers_FallbackNotInvoked()
        {
            var fallbackInvoked = false;
            var fallback = VardPolicy.Handle<Exception>().Fallback<string>(
                () => { fallbackInvoked = true; return "fallback_degraded"; });
            var retryAttempts = 0;
            var retry = VardPolicy.Handle<HttpRequestException>().Retry(3,
                onRetry: (_, _, attempt, _, _) => { Interlocked.Increment(ref retryAttempts); });
            var cb = VardPolicy.Handle<HttpRequestException>().CircuitBreaker(5, TimeSpan.FromSeconds(5));
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(2));
            var bulkhead = VardPolicy.Bulkhead(5, 5);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            var executionCount = 0;
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                var current = Interlocked.Increment(ref executionCount);
                if (current == 1)
                {
                    throw new HttpRequestException("transient network glitch");
                }

                await Task.Delay(10, ct);
                return "recovered_data";
            });

            result.Should().Be("recovered_data");
            executionCount.Should().Be(2);
            retryAttempts.Should().Be(1);
            fallbackInvoked.Should().BeFalse();
            cb.CircuitState.Should().Be(CircuitState.Closed);
        }

        [Fact]
        public async Task CorporatePipeline_PersistentError_RetryExhausts_FallbackReturnsDegradedValue()
        {
            var fallback = VardPolicy.Handle<HttpRequestException>().Fallback<string>("fallback_degraded");
            var retry = VardPolicy.Handle<HttpRequestException>().Retry(2);
            var cb = VardPolicy.Handle<HttpRequestException>().CircuitBreaker(5, TimeSpan.FromSeconds(5));
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(2));
            var bulkhead = VardPolicy.Bulkhead(5, 5);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            var attempts = 0;
            var result = await pipeline.ExecuteAsync<string>(async ct =>
            {
                Interlocked.Increment(ref attempts);
                await Task.Delay(5, ct);
                throw new HttpRequestException("permanent downstream error");
            });

            result.Should().Be("fallback_degraded");
            attempts.Should().Be(3); // 1 initial + 2 retries
        }

        [Fact]
        public async Task CorporatePipeline_CircuitBreakerOpen_FailsFast_FallbackCatchesOpenException()
        {
            var fallback = VardPolicy.Handle<CircuitBreakerOpenException>().Fallback<string>("cb_open_contingency");
            var retry = VardPolicy.Handle<HttpRequestException>().Retry(1);
            var cb = VardPolicy.Handle<HttpRequestException>().CircuitBreaker(2, TimeSpan.FromSeconds(10));
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(2));
            var bulkhead = VardPolicy.Bulkhead(5, 5);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            // Trip the circuit breaker by producing 2 failures
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await cb.ExecuteAsync<int>(_ => throw new HttpRequestException());
                }
                catch { }
            }

            cb.CircuitState.Should().Be(CircuitState.Open);

            var downstreamReached = false;
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                downstreamReached = true;
                await Task.Delay(10, ct);
                return "should_not_reach";
            });

            result.Should().Be("cb_open_contingency");
            downstreamReached.Should().BeFalse();
        }

        [Fact]
        public async Task CorporatePipeline_DownstreamSlow_TimeoutTriggers_RetryRetries_FallbackHandles()
        {
            var fallback = VardPolicy.Handle<TimeoutRejectedException>().Fallback<string>("fallback_after_timeouts");
            var retry = VardPolicy.Handle<TimeoutRejectedException>().Retry(2);
            var cb = VardPolicy.Handle<TimeoutRejectedException>().CircuitBreaker(5, TimeSpan.FromSeconds(5));
            var timeout = VardPolicy.Timeout(TimeSpan.FromMilliseconds(40));
            var bulkhead = VardPolicy.Bulkhead(5, 5);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            var executions = 0;
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                Interlocked.Increment(ref executions);
                await Task.Delay(300, ct); // Always exceeds 40ms timeout
                return "too_late";
            });

            result.Should().Be("fallback_after_timeouts");
            executions.Should().Be(3); // 1 initial + 2 retries
        }

        [Fact]
        public async Task CorporatePipeline_ConcurrencySaturation_BulkheadRejects_FallbackHandles()
        {
            var fallback = VardPolicy.Handle<BulkheadRejectedException>().Fallback<string>("bulkhead_saturated_fallback");
            var retry = VardPolicy.Handle<HttpRequestException>().Retry(1);
            var cb = VardPolicy.Handle<HttpRequestException>().CircuitBreaker(5, TimeSpan.FromSeconds(5));
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(2));
            // Bulkhead with capacity 1 and 0 queue slots
            var bulkhead = VardPolicy.Bulkhead(1, 0);

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            using var barrier = new Barrier(2);
            var task1Started = new TaskCompletionSource<bool>();

            // Task 1 occupies the slot
            var task1 = Task.Run(async () =>
            {
                return await pipeline.ExecuteAsync(async ct =>
                {
                    task1Started.TrySetResult(true);
                    barrier.SignalAndWait();
                    await Task.Delay(100, ct);
                    return "task1_success";
                });
            });

            await task1Started.Task;

            // Task 2 should be rejected by Bulkhead and caught by Fallback
            var task2Result = await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                return "task2_normal";
            });

            barrier.SignalAndWait();
            await task1;

            task2Result.Should().Be("bulkhead_saturated_fallback");
        }

        [Fact]
        public async Task CorporatePipeline_SharedContext_EnrichedAcrossAllLayers()
        {
            var fallback = VardPolicy.Handle<Exception>()
                .Fallback<string>(
                    fallbackValue: "fallback_val",
                    onFallback: (ex, ctx) =>
                    {
                        if (ctx != null) ctx["Layer_Fallback"] = true;
                    });

            var retry = VardPolicy.Handle<HttpRequestException>()
                .Retry(
                    retryCount: 2,
                    onRetry: (ex, res, att, delay, ctx) =>
                    {
                        if (ctx != null) ctx[$"Layer_Retry_{att}"] = true;
                    });

            var cb = VardPolicy.Handle<HttpRequestException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 10,
                    durationOfBreak: TimeSpan.FromSeconds(5),
                    onBreak: (ex, dur, ctx) =>
                    {
                        if (ctx != null) ctx["Layer_CircuitBreaker"] = true;
                    });

            var timeout = VardPolicy.Timeout(
                timeout: TimeSpan.FromSeconds(2),
                onTimeout: (ctx, dur) =>
                {
                    if (ctx != null) ctx["Layer_Timeout"] = true;
                });

            var bulkhead = VardPolicy.Bulkhead(
                maxParallelization: 5,
                maxQueuedActions: 5,
                onBulkheadRejected: ctx =>
                {
                    if (ctx != null) ctx["Layer_Bulkhead"] = true;
                });

            var pipeline = fallback.Wrap(retry).Wrap(cb).Wrap(timeout).Wrap(bulkhead);

            var context = new Dictionary<string, object>
            {
                ["CorrelationId"] = "trace-corp-001"
            };

            var executionCount = 0;
            var result = await pipeline.ExecuteAsync(async (ctx, ct) =>
            {
                var att = Interlocked.Increment(ref executionCount);
                ctx["Action_Reached"] = att;
                if (att == 1)
                {
                    throw new HttpRequestException("retry me");
                }
                await Task.Delay(10, ct);
                return "final_value";
            }, context);

            result.Should().Be("final_value");
            context["CorrelationId"].Should().Be("trace-corp-001");
            context.Should().ContainKey("Layer_Retry_1");
            context["Action_Reached"].Should().Be(2);
        }

        // =========================================================================
        // Parte 2: Baixa Latência (Fallback -> Hedging -> Timeout) & Sintaxe
        // =========================================================================

        [Fact]
        public async Task LowLatencyPipeline_HedgingWins_FallbackNotInvoked()
        {
            var fallbackInvoked = false;
            var fallback = VardPolicy.Handle<Exception>()
                .Fallback<string>(() =>
                {
                    fallbackInvoked = true;
                    return "fallback_emergency";
                });

            var hedging = VardPolicy.Handle<Exception>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(30));

            var timeout = VardPolicy.Timeout(TimeSpan.FromMilliseconds(150));

            var pipeline = fallback.Wrap(hedging).Wrap(timeout);

            var attempts = 0;
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                var att = Interlocked.Increment(ref attempts);
                if (att == 1)
                {
                    await Task.Delay(100, ct);
                    return "slow_1";
                }

                await Task.Delay(10, ct);
                return "hedge_fast_2";
            });

            result.Should().Be("hedge_fast_2");
            fallbackInvoked.Should().BeFalse();
            attempts.Should().Be(2);
        }

        [Fact]
        public async Task LowLatencyPipeline_AllHedgesTimeout_FallbackCatchesAndReturnsAlternative()
        {
            var fallback = VardPolicy.Handle<TimeoutRejectedException>()
                .Fallback<string>("fallback_alternative");

            var hedging = VardPolicy.Handle<TimeoutRejectedException>()
                .Hedging(maxHedges: 1, hedgingDelay: TimeSpan.FromMilliseconds(20));

            var timeout = VardPolicy.Timeout(TimeSpan.FromMilliseconds(30));

            var pipeline = fallback.Wrap(hedging).Wrap(timeout);

            var attempts = 0;
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                Interlocked.Increment(ref attempts);
                await Task.Delay(300, ct); // Exceeds 30ms timeout on both primary and hedge
                return "too_late";
            });

            result.Should().Be("fallback_alternative");
            attempts.Should().Be(2);
        }

        [Fact]
        public async Task SyntaxEquivalence_WrapExtension_Vs_VardPolicyWrap()
        {
            var p1 = VardPolicy.Handle<HttpRequestException>().Fallback("fb_equiv");
            var p2 = VardPolicy.Handle<HttpRequestException>().Retry(1);
            var p3 = VardPolicy.Timeout(TimeSpan.FromSeconds(2));

            // Wrap extension chaining: p1.Wrap(p2).Wrap(p3)
            var chained = p1.Wrap(p2).Wrap(p3);

            // Static params folding: VardPolicy.Wrap(p1, p2, p3)
            var folded = VardPolicy.Wrap(p1, p2, p3);

            var resultChained = await chained.ExecuteAsync(async ct =>
            {
                await Task.Delay(5, ct);
                return "syntax_match";
            });

            var resultFolded = await folded.ExecuteAsync(async ct =>
            {
                await Task.Delay(5, ct);
                return "syntax_match";
            });

            resultChained.Should().Be("syntax_match");
            resultFolded.Should().Be("syntax_match");
        }

        [Fact]
        public async Task MultiPolicyPipeline_ConcurrencyStress_ThreadSafeWithoutDeadlocks()
        {
            var fallback = VardPolicy.Handle<Exception>().Fallback<int>(-1);
            var retry = VardPolicy.Handle<InvalidOperationException>().Retry(1);
            var timeout = VardPolicy.Timeout(TimeSpan.FromMilliseconds(300));
            var bulkhead = VardPolicy.Bulkhead(10, 20);

            var pipeline = fallback.Wrap(retry).Wrap(timeout).Wrap(bulkhead);

            const int totalRequests = 40;
            var tasks = new Task<int>[totalRequests];
            var results = new ConcurrentBag<int>();

            for (int i = 0; i < totalRequests; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    return await pipeline.ExecuteAsync(async ct =>
                    {
                        if (index % 5 == 0)
                        {
                            throw new InvalidOperationException("transient_contention");
                        }
                        await Task.Delay(10, ct);
                        return index;
                    });
                });
            }

            var outcomes = await Task.WhenAll(tasks);
            outcomes.Should().HaveCount(totalRequests);

            // Zero deadlocks and valid outcomes (either positive index or -1 fallback)
            foreach (var outcome in outcomes)
            {
                outcome.Should().BeGreaterOrEqualTo(-1);
            }
        }

        [Fact]
        public async Task MultiPolicyPipeline_VoidAction_ExecutesCorrectly()
        {
            var retry = VardPolicy.Handle<InvalidOperationException>().Retry(1);
            var timeout = VardPolicy.Timeout(TimeSpan.FromSeconds(1));
            var pipeline = retry.Wrap(timeout);

            var executed = false;
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(10, ct);
                executed = true;
            });

            executed.Should().BeTrue();

            var syncExecuted = false;
            pipeline.Execute(() =>
            {
                syncExecuted = true;
            });

            syncExecuted.Should().BeTrue();
        }
    }
}
