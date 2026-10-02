using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Builders;

namespace Vard.Policies
{
    /// <summary>
    /// Estrutura interna de composição de políticas (D-16).
    /// A lógica de execução em cadeia é implementada nas Fases 2–5.
    /// </summary>
    internal sealed class PolicyWrap : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        internal readonly HandleCondition Condition;

        internal PolicyWrap(HandleCondition condition)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        // IPolicy — sync (stub: execução implementada nas Fases 2–5)
        public TResult Execute<TResult>(Func<TResult> action)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+. Use a concrete policy (Retry, Timeout, etc.).");

        public TResult Execute<TResult>(
            Func<IDictionary<string, object>, TResult> action,
            IDictionary<string, object> context)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");

        public PolicyResult<TResult> ExecuteAndCapture<TResult>(
            Func<IDictionary<string, object>, TResult> action,
            IDictionary<string, object> context)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");

        // IAsyncPolicy — async (stub)
        public Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");

        public Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("PolicyWrap execution is implemented in Phase 2+.");
    }
}
