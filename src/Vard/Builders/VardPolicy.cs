using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Ponto de entrada estático da API Vard.
    /// </summary>
    public static class VardPolicy
    {
        public static IPolicyBuilder Handle<TException>() where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle<TException>();
        }

        public static IPolicyBuilder Handle<TException>(Func<TException, bool> predicate) where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle(predicate);
        }

        public static IPolicyBuilder HandleResult<TResult>(Func<TResult, bool> predicate)
        {
            var builder = new PolicyBuilder();
            return builder.HandleResult(predicate);
        }

        public static RetryPolicy Retry(int count)
            => Handle<Exception>().Retry(count);

        public static RetryPolicy RetryWithBackoff(int count, TimeSpan initialDelay, BackoffType backoffType = BackoffType.Fixed, bool useJitter = false)
            => Handle<Exception>().RetryWithBackoff(count, initialDelay, backoffType, useJitter);

        public static TimeoutPolicy Timeout(
            TimeSpan timeout,
            Action<IDictionary<string, object>?, TimeSpan>? onTimeout = null,
            Func<IDictionary<string, object>?, TimeSpan, Task>? onTimeoutAsync = null)
            => new TimeoutPolicy(timeout, onTimeout, onTimeoutAsync);

        public static FallbackPolicy<TResult> Fallback<TResult>(TResult fallbackValue)
            => Handle<Exception>().Fallback(fallbackValue);

        public static FallbackPolicy Fallback(Action fallbackAction)
            => Handle<Exception>().Fallback(fallbackAction);

        public static CircuitBreakerPolicy CircuitBreaker(
            int exceptionsAllowedBeforeBreaking,
            TimeSpan durationOfBreak,
            Action<Exception?, TimeSpan, IDictionary<string, object>?>? onBreak = null,
            Action<IDictionary<string, object>?>? onReset = null,
            Action<IDictionary<string, object>?>? onHalfOpen = null,
            Func<Exception?, TimeSpan, IDictionary<string, object>?, Task>? onBreakAsync = null,
            Func<IDictionary<string, object>?, Task>? onResetAsync = null,
            Func<IDictionary<string, object>?, Task>? onHalfOpenAsync = null)
            => Handle<Exception>().CircuitBreaker(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                onBreak,
                onReset,
                onHalfOpen,
                onBreakAsync,
                onResetAsync,
                onHalfOpenAsync);

        public static AdvancedCircuitBreakerPolicy AdvancedCircuitBreaker(
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
            => Handle<Exception>().AdvancedCircuitBreaker(
                failureThreshold,
                samplingDuration,
                minimumThroughput,
                durationOfBreak,
                onBreak,
                onReset,
                onHalfOpen,
                onBreakAsync,
                onResetAsync,
                onHalfOpenAsync);

        public static BulkheadPolicy Bulkhead(
            int maxParallelization,
            int maxQueuedActions = 0,
            Action<IDictionary<string, object>?>? onBulkheadRejected = null,
            Func<IDictionary<string, object>?, Task>? onBulkheadRejectedAsync = null)
            => new BulkheadPolicy(maxParallelization, maxQueuedActions, onBulkheadRejected, onBulkheadRejectedAsync);

        public static TokenBucketRateLimiterPolicy TokenBucket(
            int maxTokens,
            double tokensPerSecond,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
            => new TokenBucketRateLimiterPolicy(
                maxTokens,
                tokensPerSecond,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);

        public static SlidingWindowRateLimiterPolicy SlidingWindow(
            int permitLimit,
            TimeSpan windowDuration,
            int segmentsPerWindow = 10,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
            => new SlidingWindowRateLimiterPolicy(
                permitLimit,
                windowDuration,
                segmentsPerWindow,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);
    }
}
