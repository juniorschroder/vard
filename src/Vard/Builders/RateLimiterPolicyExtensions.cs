using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para configuração de políticas de Rate Limiter.
    /// </summary>
    public static class RateLimiterPolicyExtensions
    {
        public static TokenBucketRateLimiterPolicy TokenBucketRateLimiter(
            this IPolicyBuilder builder,
            int maxTokens,
            double tokensPerSecond,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new TokenBucketRateLimiterPolicy(
                maxTokens,
                tokensPerSecond,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);
        }

        public static SlidingWindowRateLimiterPolicy SlidingWindowRateLimiter(
            this IPolicyBuilder builder,
            int permitLimit,
            TimeSpan windowDuration,
            int segmentsPerWindow = 10,
            TimeSpan? maxWaitTime = null,
            Action<TimeSpan, IDictionary<string, object>?>? onRateLimitExceeded = null,
            Func<TimeSpan, IDictionary<string, object>?, Task>? onRateLimitExceededAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new SlidingWindowRateLimiterPolicy(
                permitLimit,
                windowDuration,
                segmentsPerWindow,
                maxWaitTime,
                onRateLimitExceeded,
                onRateLimitExceededAsync);
        }
    }
}
