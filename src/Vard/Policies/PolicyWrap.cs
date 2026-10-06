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
        /// <summary>
        /// Obtém a política externa do pipeline (executada primeiro).
        /// </summary>
        public IPolicy? OuterPolicy { get; }

        /// <summary>
        /// Obtém a política interna do pipeline (executada internamente).
        /// </summary>
        public IPolicy? InnerPolicy { get; }
        internal readonly HandleCondition? Condition;

        /// <summary>
        /// Inicializa uma nova instância de <see cref="PolicyWrap"/> combinando duas políticas em sequência aninhada.
        /// </summary>
        /// <param name="outerPolicy">A política externa que envolve a execução.</param>
        /// <param name="innerPolicy">A política interna envolvida na execução.</param>
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
        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> action)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+. Use a concrete policy (Retry, Timeout, etc.).");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.Execute(() => InnerPolicy.Execute(action));
        }

        /// <inheritdoc />
        public TResult Execute<TResult>(
            Func<IDictionary<string, object>, TResult> action,
            IDictionary<string, object> context)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.Execute(ctx => InnerPolicy.Execute(action, ctx), context);
        }

        /// <inheritdoc />
        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
        {
            if (OuterPolicy == null || InnerPolicy == null)
                throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            return OuterPolicy.ExecuteAndCapture(() => InnerPolicy.Execute(action));
        }

        /// <inheritdoc />
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
        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
