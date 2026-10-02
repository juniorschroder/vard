using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Common;
using Vard.Policies;

namespace Vard.Builders
{
    public static class RetryPolicyExtensions
    {
        public static RetryPolicy Retry(this IPolicyBuilder builder, int retryCount)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount);
        }

        public static RetryPolicy Retry(
            this IPolicyBuilder builder,
            int retryCount,
            Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?> onRetry)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount, onRetrySync: onRetry);
        }

        public static RetryPolicy Retry(
            this IPolicyBuilder builder,
            int retryCount,
            Func<int, TimeSpan> sleepDurationProvider)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(b.Condition, retryCount, sleepDurationProvider: (attempt, _) => sleepDurationProvider(attempt));
        }

        public static RetryPolicy RetryWithBackoff(
            this IPolicyBuilder builder,
            int retryCount,
            TimeSpan initialDelay,
            BackoffType backoffType = BackoffType.Fixed,
            bool useJitter = false,
            Action<Exception?, object?, int, TimeSpan, IDictionary<string, object>?>? onRetry = null,
            Func<Exception?, object?, int, TimeSpan, IDictionary<string, object>?, Task>? onRetryAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new RetryPolicy(
                b.Condition,
                retryCount,
                sleepDurationProvider: (attempt, _) => BackoffHelper.CalculateDelay(attempt, initialDelay, backoffType, useJitter),
                onRetrySync: onRetry,
                onRetryAsync: onRetryAsync);
        }
    }
}
