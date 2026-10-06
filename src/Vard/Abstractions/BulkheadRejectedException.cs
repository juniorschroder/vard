using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando a política de Bulkhead rejeita uma execução por saturação de slots ou fila cheia.
    /// </summary>
    public class BulkheadRejectedException : Exception
    {
        /// <summary>
        /// Obtém o número máximo de execuções paralelas permitidas pela política.
        /// </summary>
        public int MaxParallelization { get; }

        /// <summary>
        /// Obtém o número máximo de ações enfileiradas permitidas pela política.
        /// </summary>
        public int MaxQueuedActions { get; }

        /// <summary>
        /// Obtém o motivo específico da rejeição pelo bulkhead.
        /// </summary>
        public BulkheadRejectionReason Reason { get; }

        /// <summary>
        /// Inicializa uma nova instância de <see cref="BulkheadRejectedException"/>.
        /// </summary>
        /// <param name="maxParallelization">O limite configurado de execuções concorrentes.</param>
        /// <param name="maxQueuedActions">O limite configurado de itens na fila.</param>
        /// <param name="reason">A causa da rejeição (<see cref="BulkheadRejectionReason"/>).</param>
        /// <param name="message">Mensagem descritiva opcional.</param>
        /// <param name="innerException">Exceção interna opcional.</param>
        public BulkheadRejectedException(
            int maxParallelization,
            int maxQueuedActions,
            BulkheadRejectionReason reason,
            string? message = null,
            Exception? innerException = null)
            : base(message ?? $"The bulkhead execution limit was reached (MaxParallelization: {maxParallelization}, MaxQueuedActions: {maxQueuedActions}, Reason: {reason}).", innerException)
        {
            MaxParallelization = maxParallelization;
            MaxQueuedActions = maxQueuedActions;
            Reason = reason;
        }
    }
}
