using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vard.Abstractions;
using Void = Vard.Abstractions.Void;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para simplificar a execução de ações void (sem retorno) em políticas.
    /// Utiliza o sentinel <see cref="Void"/> internamente (D-03).
    /// </summary>
    public static class PolicyActionExtensions
    {
        public static void Execute(this IPolicy policy, Action action)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            policy.Execute<Void>(() =>
            {
                action();
                return Void.Instance;
            });
        }

        public static void Execute(
            this IPolicy policy,
            Action<IDictionary<string, object>> action,
            IDictionary<string, object> context)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            policy.Execute<Void>(ctx =>
            {
                action(ctx);
                return Void.Instance;
            }, context);
        }

        public static PolicyResult<Void> ExecuteAndCapture(this IPolicy policy, Action action)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAndCapture<Void>(() =>
            {
                action();
                return Void.Instance;
            });
        }

        public static PolicyResult<Void> ExecuteAndCapture(
            this IPolicy policy,
            Action<IDictionary<string, object>> action,
            IDictionary<string, object> context)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAndCapture<Void>(ctx =>
            {
                action(ctx);
                return Void.Instance;
            }, context);
        }

        public static Task ExecuteAsync(
            this IAsyncPolicy policy,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAsync<Void>(async ct =>
            {
                await action(ct).ConfigureAwait(false);
                return Void.Instance;
            }, cancellationToken);
        }

        public static Task ExecuteAsync(
            this IAsyncPolicy policy,
            Func<IDictionary<string, object>, CancellationToken, Task> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAsync<Void>(async (ctx, ct) =>
            {
                await action(ctx, ct).ConfigureAwait(false);
                return Void.Instance;
            }, context, cancellationToken);
        }

        public static Task<PolicyResult<Void>> ExecuteAndCaptureAsync(
            this IAsyncPolicy policy,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAndCaptureAsync<Void>(async ct =>
            {
                await action(ct).ConfigureAwait(false);
                return Void.Instance;
            }, cancellationToken);
        }

        public static Task<PolicyResult<Void>> ExecuteAndCaptureAsync(
            this IAsyncPolicy policy,
            Func<IDictionary<string, object>, CancellationToken, Task> action,
            IDictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return policy.ExecuteAndCaptureAsync<Void>(async (ctx, ct) =>
            {
                await action(ctx, ct).ConfigureAwait(false);
                return Void.Instance;
            }, context, cancellationToken);
        }
    }
}
