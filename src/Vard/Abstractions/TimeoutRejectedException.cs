using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Exceção lançada quando uma execução excede o tempo limite configurado na política de timeout.
    /// </summary>
    public class TimeoutRejectedException : Exception
    {
        public TimeSpan Timeout { get; }

        public TimeoutRejectedException(TimeSpan timeout)
            : base($"The execution has timed out after {timeout.TotalMilliseconds}ms.")
        {
            Timeout = timeout;
        }

        public TimeoutRejectedException(TimeSpan timeout, string message)
            : base(message)
        {
            Timeout = timeout;
        }

        public TimeoutRejectedException(TimeSpan timeout, string message, Exception innerException)
            : base(message, innerException)
        {
            Timeout = timeout;
        }
    }
}
