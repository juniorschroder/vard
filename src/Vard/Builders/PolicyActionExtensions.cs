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
        /// <summary>
        /// Executa uma ação síncrona sem retorno protegida pela política.
        /// </summary>
        /// <param name="policy">A política de resiliência a ser aplicada.</param>
        /// <param name="action">A ação sem retorno a ser executada.</param>
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

        /// <summary>
        /// Executa uma ação síncrona com contexto sem retorno protegida pela política.
        /// </summary>
        /// <param name="policy">A política de resiliência a ser aplicada.</param>
        /// <param name="action">A ação sem retorno a ser executada com contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado.</param>
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

        /// <summary>
        /// Executa uma ação síncrona sem retorno e captura o resultado da execução.
        /// </summary>
        /// <param name="policy">A política de resiliência a ser aplicada.</param>
        /// <param name="action">A ação sem retorno a ser executada.</param>
        /// <returns>Um <see cref="PolicyResult{Void}"/> contendo o status da execução.</returns>
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

        /// <summary>
        /// Executa uma ação síncrona com contexto sem retorno e captura o resultado da execução.
        /// </summary>
        /// <param name="policy">A política de resiliência a ser aplicada.</param>
        /// <param name="action">A ação sem retorno a ser executada com contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado.</param>
        /// <returns>Um <see cref="PolicyResult{Void}"/> contendo o status da execução.</returns>
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

        /// <summary>
        /// Executa uma ação assíncrona sem retorno protegida pela política.
        /// </summary>
        /// <param name="policy">A política assíncrona a ser aplicada.</param>
        /// <param name="action">A ação assíncrona sem retorno a ser executada.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa representando a execução assíncrona.</returns>
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

        /// <summary>
        /// Executa uma ação assíncrona com contexto sem retorno protegida pela política.
        /// </summary>
        /// <param name="policy">A política assíncrona a ser aplicada.</param>
        /// <param name="action">A ação assíncrona sem retorno a ser executada com contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa representando a execução assíncrona.</returns>
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

        /// <summary>
        /// Executa uma ação assíncrona sem retorno e captura o resultado da execução.
        /// </summary>
        /// <param name="policy">A política assíncrona a ser aplicada.</param>
        /// <param name="action">A ação assíncrona sem retorno a ser executada.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa contendo o <see cref="PolicyResult{Void}"/> da execução.</returns>
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

        /// <summary>
        /// Executa uma ação assíncrona com contexto sem retorno e captura o resultado da execução.
        /// </summary>
        /// <param name="policy">A política assíncrona a ser aplicada.</param>
        /// <param name="action">A ação assíncrona sem retorno a ser executada com contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa contendo o <see cref="PolicyResult{Void}"/> da execução.</returns>
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
