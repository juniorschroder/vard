using System;
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

        public static TimeoutPolicy Timeout(TimeSpan timeout)
            => new TimeoutPolicy(timeout);

        public static FallbackPolicy<TResult> Fallback<TResult>(TResult fallbackValue)
            => Handle<Exception>().Fallback(fallbackValue);

        public static FallbackPolicy Fallback(Action fallbackAction)
            => Handle<Exception>().Fallback(fallbackAction);
    }
}
