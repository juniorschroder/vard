using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato para políticas de Circuit Breaker com controle de estado e comandos manuais.
    /// </summary>
    public interface ICircuitBreakerPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        /// <summary>
        /// Estado atual do circuito.
        /// </summary>
        CircuitState CircuitState { get; }

        /// <summary>
        /// Última exceção tratada que causou a abertura do circuito, se houver.
        /// </summary>
        Exception? LastException { get; }

        /// <summary>
        /// Isola manualmente o circuito no estado Isolated, rejeitando todas as chamadas até Reset() ser invocado.
        /// </summary>
        void Isolate();

        /// <summary>
        /// Fecha manualmente o circuito no estado Closed, resetando contadores de falhas.
        /// </summary>
        void Reset();
    }
}
