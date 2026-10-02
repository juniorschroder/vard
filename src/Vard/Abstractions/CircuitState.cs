namespace Vard.Abstractions
{
    /// <summary>
    /// Estados do Circuit Breaker.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuito fechado: operações fluem normalmente.
        /// </summary>
        Closed = 0,

        /// <summary>
        /// Circuito aberto: operações são bloqueadas por terem excedido limites de falha.
        /// </summary>
        Open = 1,

        /// <summary>
        /// Circuito meio-aberto: período de prova com chamada piloto para testar recuperação.
        /// </summary>
        HalfOpen = 2,

        /// <summary>
        /// Circuito isolado manualmente: permanece aberto indefinidamente até Reset() explícito.
        /// </summary>
        Isolated = 3
    }
}
