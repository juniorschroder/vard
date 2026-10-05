using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para configuração de política de Hedging no IPolicyBuilder.
    /// </summary>
    public static class HedgingPolicyExtensions
    {
        public static HedgingPolicy Hedging(
            this IPolicyBuilder builder,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy(
                b.Condition,
                maxHedges,
                hedgingDelay,
                onHedgingResult,
                onHedgingResultAsync);
        }

        public static HedgingPolicy Hedging(
            this IPolicyBuilder builder,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<object?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<object?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy(
                b.Condition,
                maxHedges,
                hedgingDelayProvider,
                onHedgingResult,
                onHedgingResultAsync);
        }

        public static HedgingPolicy<TResult> Hedging<TResult>(
            this IPolicyBuilder builder,
            int maxHedges = 1,
            TimeSpan hedgingDelay = default,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy<TResult>(
                b.Condition,
                maxHedges,
                hedgingDelay,
                onHedgingResult,
                onHedgingResultAsync);
        }

        public static HedgingPolicy<TResult> Hedging<TResult>(
            this IPolicyBuilder builder,
            int maxHedges,
            Func<int, TimeSpan> hedgingDelayProvider,
            Action<TResult?, int, TimeSpan, IDictionary<string, object>?>? onHedgingResult = null,
            Func<TResult?, int, TimeSpan, IDictionary<string, object>?, Task>? onHedgingResultAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var b = (PolicyBuilder)builder;
            return new HedgingPolicy<TResult>(
                b.Condition,
                maxHedges,
                hedgingDelayProvider,
                onHedgingResult,
                onHedgingResultAsync);
        }
    }
}
