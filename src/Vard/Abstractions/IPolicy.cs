using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato de execução síncrona de política.
    /// </summary>
    public interface IPolicy
    {
        /// <summary>
        /// Executa uma ação síncrona dentro do pipeline protegido pela política.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da ação.</typeparam>
        /// <param name="action">A função síncrona a ser executada.</param>
        /// <returns>O resultado retornado pela ação.</returns>
        TResult Execute<TResult>(Func<TResult> action);

        /// <summary>
        /// Executa uma ação síncrona com contexto dentro do pipeline protegido pela política.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da ação.</typeparam>
        /// <param name="action">A função síncrona a ser executada recebendo o contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado para a execução.</param>
        /// <returns>O resultado retornado pela ação.</returns>
        TResult Execute<TResult>(Func<System.Collections.Generic.IDictionary<string, object>, TResult> action,
                                 System.Collections.Generic.IDictionary<string, object> context);

        /// <summary>
        /// Executa uma ação síncrona e captura o resultado ou exceções em um <see cref="PolicyResult{TResult}"/>.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da ação.</typeparam>
        /// <param name="action">A função síncrona a ser executada.</param>
        /// <returns>Um <see cref="PolicyResult{TResult}"/> contendo informações detalhadas da execução.</returns>
        PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action);

        /// <summary>
        /// Executa uma ação síncrona com contexto e captura o resultado ou exceções em um <see cref="PolicyResult{TResult}"/>.
        /// </summary>
        /// <typeparam name="TResult">O tipo de retorno da ação.</typeparam>
        /// <param name="action">A função síncrona a ser executada recebendo o contexto.</param>
        /// <param name="context">O dicionário de contexto compartilhado para a execução.</param>
        /// <returns>Um <see cref="PolicyResult{TResult}"/> contendo informações detalhadas da execução.</returns>
        PolicyResult<TResult> ExecuteAndCapture<TResult>(
            Func<System.Collections.Generic.IDictionary<string, object>, TResult> action,
            System.Collections.Generic.IDictionary<string, object> context);
    }
}
