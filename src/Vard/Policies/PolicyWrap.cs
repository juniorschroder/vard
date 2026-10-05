using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Policies
{
    /// <summary>
    /// Composição operacional de políticas de resiliência encadeadas de fora para dentro.
    /// OuterPolicy encapsula a execução de InnerPolicy.
    /// </summary>
    public class PolicyWrap : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        public IPolicy? OuterPolicy { get; }
        public IPolicy? InnerPolicy { get; }
        internal readonly HandleCondition? Condition;

        public PolicyWrap(IPolicy outerPolicy, IPolicy innerPolicy)
        {
            OuterPolicy = outerPolicy ?? throw new ArgumentNullException(nameof(outerPolicy));
            InnerPolicy = innerPolicy ?? throw new ArgumentNullException(nameof(innerPolicy));
        }

        // Backward compatibility constructor for Phase 1 placeholder
        internal PolicyWrap(HandleCondition condition)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        // IPolicy — sync
        public TResult Execute<TResult>(Func<TResult> action)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+. Use a concrete policy (Retry, Timeout, etc.).");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.Execute(() => InnerPolicy.Execute(action));
        }

        public TResult Execute<TResult>(
            Func<IDictionary<string, object>, TResult> action,
            IDictionary<string, object> context)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.Execute(ctx => InnerPolicy.Execute(action, ctx), context);
        }

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.ExecuteAndCapture(() => InnerPolicy.Execute(action));
        }

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(
            Func<IDictionary<string, object>, TResult> action,
            IDictionary<string, object> context)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.ExecuteAndCapture(ctx => InnerPolicy.Execute(action, ctx), context);
        }

        // IAsyncPolicy — async
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!(OuterPolicy is IAsyncPolicy asyncOuter) || !(InnerPolicy is IAsyncPolicy asyncInner))
                throw new InvalidOperationException("Both outer and inner policies must implement IAsyncPolicy for asynchronous execution.");
            return asyncOuter.ExecuteAsync(ct => asyncInner.ExecuteAsync(action, ct), cancellationToken);
        }

        public Task<TResult> ExecuteAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!(OuterPolicy is IAsyncPolicy asyncOuter) || !(InnerPolicy is IAsyncPolicy asyncInner))
                throw new InvalidOperationException("Both outer and inner policies must implement IAsyncPolicy for asynchronous execution.");
            return asyncOuter.ExecuteAsync((ctx, ct) => asyncInner.ExecuteAsync(action, ctx, ct), context, cancellationToken);
        }

        public Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!(OuterPolicy is IAsyncPolicy asyncOuter) || !(InnerPolicy is IAsyncPolicy asyncInner))
                throw new InvalidOperationException("Both outer and inner policies must implement IAsyncPolicy for asynchronous execution.");
            return asyncOuter.ExecuteAndCaptureAsync(ct => asyncInner.ExecuteAsync(action, ct), cancellationToken);
        }

        public Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!(OuterPolicy is IAsyncPolicy asyncOuter) || !(InnerPolicy is IAsyncPolicy asyncInner))
                throw new InvalidOperationException("Both outer and inner policies must implement IAsyncPolicy for asynchronous execution.");
            return asyncOuter.ExecuteAndCaptureAsync((ctx, ct) => asyncInner.ExecuteAsync(action, ctx, ct), context, cancellationToken);
        }
    }
}
