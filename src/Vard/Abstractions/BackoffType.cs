namespace Vard.Abstractions
{
    /// <summary>
    /// Define o algoritmo de cálculo de intervalo entre tentativas de retry.
    /// </summary>
    public enum BackoffType
    {
        /// <summary>Intervalo constante entre cada tentativa.</summary>
        Fixed = 0,

        /// <summary>Intervalo cresce linearmente (initialDelay * attempt).</summary>
        Linear = 1,

        /// <summary>Intervalo cresce exponencialmente (initialDelay * 2^(attempt-1)).</summary>
        Exponential = 2
    }
}
