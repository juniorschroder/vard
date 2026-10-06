using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando uma execução excede o tempo limite configurado na política de timeout.
    /// </summary>
    public class TimeoutRejectedException : Exception
    {
        /// <summary>
        /// Obtém a duração limite configurada que foi excedida.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Inicializa uma nova instância de <see cref="TimeoutRejectedException"/> com a duração limite excedida.
        /// </summary>
        /// <param name="timeout">A duração limite configurada.</param>
        public TimeoutRejectedException(TimeSpan timeout)
            : base($"The execution has timed out after {timeout.TotalMilliseconds}ms.")
        {
            Timeout = timeout;
        }

        /// <summary>
        /// Inicializa uma nova instância de <see cref="TimeoutRejectedException"/> com a duração limite e mensagem personalizada.
        /// </summary>
        /// <param name="timeout">A duração limite configurada.</param>
        /// <param name="message">A mensagem de erro que explica o motivo da exceção.</param>
        public TimeoutRejectedException(TimeSpan timeout, string message)
            : base(message)
        {
            Timeout = timeout;
        }

        /// <summary>
        /// Inicializa uma nova instância de <see cref="TimeoutRejectedException"/> com duração limite, mensagem e causa interna.
        /// </summary>
        /// <param name="timeout">A duração limite configurada.</param>
        /// <param name="message">A mensagem de erro que explica o motivo da exceção.</param>
        /// <param name="innerException">A exceção de causa raiz que antecedeu o timeout.</param>
        public TimeoutRejectedException(TimeSpan timeout, string message, Exception innerException)
            : base(message, innerException)
        {
            Timeout = timeout;
        }
    }
}
