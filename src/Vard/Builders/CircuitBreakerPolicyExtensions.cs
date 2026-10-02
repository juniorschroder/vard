using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    public static class CircuitBreakerPolicyExtensions
    {
        public static CircuitBreakerPolicy CircuitBreaker(
            this IPolicyBuilder builder,
            int exceptionsAllowedBeforeBreaking,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new CircuitBreakerPolicy(
                b.Condition,
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                onBreak,
                onBreakAsync,
                onReset,
                onResetAsync,
                onHalfOpen,
                onHalfOpenAsync);
        }

        public static AdvancedCircuitBreakerPolicy AdvancedCircuitBreaker(
            this IPolicyBuilder builder,
            double failureThreshold,
            TimeSpan samplingDuration,
            int minimumThroughput,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new AdvancedCircuitBreakerPolicy(
                b.Condition,
                failureThreshold,
                samplingDuration,
                minimumThroughput,
                durationOfBreak,
                onBreak,
                onBreakAsync,
                onReset,
                onResetAsync,
                onHalfOpen,
                onHalfOpenAsync);
        }
    }
}
