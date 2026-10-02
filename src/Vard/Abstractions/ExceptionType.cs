namespace Vard.Abstractions
{
    /// <summary>
    /// Classifica o motivo de falha de uma execução de política.
    /// </summary>
    public enum ExceptionType
    {
        /// <summary>Execução bem-sucedida — nenhuma exceção ou resultado inesperado.</summary>
        None = 0,

        /// <summary>Falhou por exceção que o HandleException capturou.</summary>
        HandledByCondition = 1,

        /// <summary>Falhou por resultado que o HandleResult capturou.</summary>
        HandledByResult = 2,

        /// <summary>Exceção não coberta por nenhuma condição configurada.</summary>
        Unhandled = 3,

        /// <summary>Política não aplicou (ex: circuito aberto, bulkhead cheio).</summary>
        PolicyBypassed = 4
    }
}
