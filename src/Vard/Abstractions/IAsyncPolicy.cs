using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato de execução assíncrona de política.
    /// </summary>
    public interface IAsyncPolicy
    {
        /// <summary>
        /// Executa uma ação assíncrona dentro do pipeline protegido pela política.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da tarefa.</typeparam>
        /// <param name="action">A função assíncrona a ser executada com token de cancelamento.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa representando a execução e contendo o resultado da ação.</returns>
        Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executa uma ação assíncrona com contexto dentro do pipeline protegido pela política.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da tarefa.</typeparam>
        /// <param name="action">A função assíncrona a ser executada com contexto e token de cancelamento.</param>
        /// <param name="context">O dicionário de contexto compartilhado para a execução.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa representando a execução e contendo o resultado da ação.</returns>
        Task<TResult> ExecuteAsync<TResult>(
            Func<System.Collections.Generic.IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            System.Collections.Generic.IDictionary<string, object> context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executa uma ação assíncrona e captura o resultado ou exceções em um <see cref="PolicyResult{TResult}"/>.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da tarefa.</typeparam>
        /// <param name="action">A função assíncrona a ser executada com token de cancelamento.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa contendo o <see cref="PolicyResult{TResult}"/> da execução.</returns>
        Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executa uma ação assíncrona com contexto e captura o resultado ou exceções em um <see cref="PolicyResult{TResult}"/>.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da tarefa.</typeparam>
        /// <param name="action">A função assíncrona a ser executada com contexto e token de cancelamento.</param>
        /// <param name="context">O dicionário de contexto compartilhado para a execução.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns>Uma tarefa contendo o <see cref="PolicyResult{TResult}"/> da execução.</returns>
        Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<System.Collections.Generic.IDictionary<string, object>, CancellationToken, Task<TResult>> action,
            System.Collections.Generic.IDictionary<string, object> context,
            CancellationToken cancellationToken = default);

    }
}
