namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato de política de isolamento de concorrência Bulkhead.
    /// </summary>
    public interface IBulkheadPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        /// <summary>
        /// Obtém a capacidade máxima de paralelismo de execução simultânea.
        /// </summary>
        int MaxParallelization { get; }

        /// <summary>
        /// Obtém a capacidade máxima da fila de espera para execuções quando os slots estão esgotados.
        /// </summary>
        int MaxQueuedActions { get; }

        /// <summary>
        /// Obtém o número atual de slots de execução disponíveis.
        /// </summary>
        int BulkheadAvailableCount { get; }

        /// <summary>
        /// Obtém o número atual de vagas disponíveis na fila de espera.
        /// </summary>
        int QueueAvailableCount { get; }
    }
}
