using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    public static class FallbackPolicyExtensions
    {
        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            TResult fallbackValue,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (_, _, _) => fallbackValue, onFallbackSync: onFallback);
        }

        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            Func<TResult> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (_, _, _) => fallbackAction(), onFallbackSync: onFallback);
        }

        public static FallbackPolicy<TResult> Fallback<TResult>(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, TResult> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(b.Condition, (ex, _, ctx) => fallbackAction(ex, ctx), onFallbackSync: onFallback);
        }

        public static FallbackPolicy<TResult> FallbackAsync<TResult>(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, CancellationToken, Task<TResult>> fallbackActionAsync,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy<TResult>(
                b.Condition,
                fallbackActionSync: null,
                fallbackActionAsync: (ex, _, ctx, ct) => fallbackActionAsync(ex, ctx, ct),
                onFallbackAsync: onFallbackAsync);
        }

        public static FallbackPolicy Fallback(
            this IPolicyBuilder builder,
            Action fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(b.Condition, (_, _) => fallbackAction(), onFallbackSync: onFallback);
        }

        public static FallbackPolicy Fallback(
            this IPolicyBuilder builder,
            Action<Exception?, IDictionary<string, object>?> fallbackAction,
            Action<Exception?, IDictionary<string, object>?>? onFallback = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(b.Condition, fallbackAction, onFallbackSync: onFallback);
        }

        public static FallbackPolicy FallbackAsync(
            this IPolicyBuilder builder,
            Func<Exception?, IDictionary<string, object>?, CancellationToken, Task> fallbackActionAsync,
            Func<Exception?, IDictionary<string, object>?, Task>? onFallbackAsync = null)
        {
            var b = (PolicyBuilder)builder;
            return new FallbackPolicy(
                b.Condition,
                fallbackActionSync: null,
                fallbackActionAsync: fallbackActionAsync,
                onFallbackAsync: onFallbackAsync);
        }
    }
}
