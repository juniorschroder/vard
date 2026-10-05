using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato para política de resiliência Hedging (execuções especulativas concorrentes).
    /// </summary>
    public interface IHedgingPolicy : IPolicy, ISyncPolicy, IAsyncPolicy
    {
        /// <summary>
        /// Quantidade máxima de execuções especulativas concorrentes além da chamada original (padrão: 1).
        /// </summary>
        int MaxHedges { get; }

        /// <summary>
        /// Delay padrão antes de disparar o próximo hedge.
        /// </summary>
        TimeSpan HedgingDelay { get; }
    }
}
