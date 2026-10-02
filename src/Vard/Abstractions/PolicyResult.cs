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

        // D-02: 8 propriedades
        public bool IsSuccess { get; }
        public T? Result { get; }
        public Exception? Exception { get; }
        public Exception? FinalException { get; }
        public ExceptionType ExceptionType { get; }
        public TimeSpan ExecutionTime { get; }
        public int AttemptNumber { get; }

        /// <summary>
        /// Contexto compartilhado pelo caller (D-04: mesma referência, não cópia).
        /// </summary>
        public IDictionary<string, object>? Context { get; }

        /// <summary>
        /// Cria um resultado de sucesso.
        /// </summary>
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
