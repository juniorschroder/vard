using System;
using System.Collections.Generic;

namespace Vard.Abstractions
{
    /// <summary>
    /// Resultado imutável de uma execução de política.
    /// Construído via factory methods estáticos <see cref="Success"/> e <see cref="Failure"/>.
    /// </summary>
    public sealed class PolicyResult<T>
    {
        // D-01: construtor privado — imutabilidade garantida
        private PolicyResult(
            bool isSuccess,
            T? result,
            Exception? exception,
            Exception? finalException,
            ExceptionType exceptionType,
            TimeSpan executionTime,
            int attemptNumber,
            IDictionary<string, object>? context)
        {
            IsSuccess = isSuccess;
            Result = result;
            Exception = exception;
            FinalException = finalException;
            ExceptionType = exceptionType;
            ExecutionTime = executionTime;
            AttemptNumber = attemptNumber;
            Context = context;
        }

        /// <summary>
        /// Indica se a execução foi concluída com sucesso.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Obtém o resultado da execução quando <see cref="IsSuccess"/> for verdadeiro.
        /// </summary>
        public T? Result { get; }

        /// <summary>
        /// Obtém a exceção capturada que disparou a política ou falha.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Obtém a exceção final após avaliação da política de resiliência.
        /// </summary>
        public Exception? FinalException { get; }

        /// <summary>
        /// Obtém a classificação da exceção capturada (<see cref="ExceptionType"/>).
        /// </summary>
        public ExceptionType ExceptionType { get; }

        /// <summary>
        /// Obtém a duração total gasta durante a execução da política.
        /// </summary>
        public TimeSpan ExecutionTime { get; }

        /// <summary>
        /// Obtém o número da tentativa que produziu este resultado.
        /// </summary>
        public int AttemptNumber { get; }

        /// <summary>
        /// Contexto compartilhado pelo caller (D-04: mesma referência, não cópia).
        /// </summary>
        public IDictionary<string, object>? Context { get; }

        /// <summary>
        /// Cria um resultado de sucesso.
        /// </summary>
        /// <param name="result">O valor de retorno da operação executada.</param>
        /// <param name="context">O contexto compartilhado da execução.</param>
        /// <param name="executionTime">O tempo total decorrido na execução.</param>
        /// <param name="attemptNumber">O número da tentativa bem-sucedida.</param>
        /// <returns>Uma nova instância de <see cref="PolicyResult{T}"/> indicando sucesso.</returns>
        public static PolicyResult<T> Success(
            T result,
            IDictionary<string, object>? context = null,
            TimeSpan executionTime = default,
            int attemptNumber = 1)
        {
            return new PolicyResult<T>(
                isSuccess: true,
                result: result,
                exception: null,
                finalException: null,
                exceptionType: ExceptionType.None,
                executionTime: executionTime,
                attemptNumber: attemptNumber,
                context: context);
        }

        /// <summary>
        /// Cria um resultado de falha.
        /// </summary>
        /// <param name="exception">A exceção capturada que causou a falha.</param>
        /// <param name="exceptionType">A classificação da exceção em relação às regras da política.</param>
        /// <param name="context">O contexto compartilhado da execução.</param>
        /// <param name="executionTime">O tempo total decorrido até a falha.</param>
        /// <param name="attemptNumber">O número de tentativas realizadas.</param>
        /// <param name="finalException">A exceção final a ser exposta (padrão: mesma que <paramref name="exception"/>).</param>
        /// <returns>Uma nova instância de <see cref="PolicyResult{T}"/> indicando falha.</returns>
        public static PolicyResult<T> Failure(
            Exception exception,
            ExceptionType exceptionType,
            IDictionary<string, object>? context = null,
            TimeSpan executionTime = default,
            int attemptNumber = 1,
            Exception? finalException = null)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            return new PolicyResult<T>(
                isSuccess: false,
                result: default,
                exception: exception,
                finalException: finalException ?? exception,
                exceptionType: exceptionType,
                executionTime: executionTime,
                attemptNumber: attemptNumber,
                context: context);
        }
    }
}
