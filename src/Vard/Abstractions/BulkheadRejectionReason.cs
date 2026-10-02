namespace Vard.Abstractions
{
    /// <summary>
    /// Especifica o motivo da rejeição de uma ação pela política de Bulkhead.
    /// </summary>
    public enum BulkheadRejectionReason
    {
        /// <summary>
        /// Todos os slots de paralelismo estão ocupados e não há fila configurada.
        /// </summary>
        ExecutionSaturated = 1,

        /// <summary>
        /// A fila de espera de ações atingiu a capacidade máxima configurada.
        /// </summary>
        QueueFull = 2
    }
}
