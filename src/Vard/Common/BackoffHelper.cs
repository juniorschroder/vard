using System;
using Vard.Abstractions;

namespace Vard.Common
{
    /// <summary>
    /// Utilitário interno para cálculo de delays de repetição.
    /// </summary>
    internal static class BackoffHelper
    {
        /// <summary>
        /// Calcula o delay para a tentativa informada (attempt base 1).
        /// </summary>
        public static TimeSpan CalculateDelay(
            int attempt,
            TimeSpan initialDelay,
            BackoffType backoffType,
            bool useJitter,
            TimeSpan? maxDelay = null)
        {
            if (attempt < 1) attempt = 1;

            double calculatedMs = backoffType switch
            {
                BackoffType.Fixed => initialDelay.TotalMilliseconds,
                BackoffType.Linear => initialDelay.TotalMilliseconds * attempt,
                BackoffType.Exponential => initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1),
                _ => initialDelay.TotalMilliseconds
            };

            if (maxDelay.HasValue && calculatedMs > maxDelay.Value.TotalMilliseconds)
            {
                calculatedMs = maxDelay.Value.TotalMilliseconds;
            }

            if (useJitter)
            {
                // Full Jitter (AWS style): random uniformemente distribuído entre 0 e o delay calculado
                calculatedMs *= ThreadSafeRandom.NextDouble();
            }

            return TimeSpan.FromMilliseconds(calculatedMs);
        }
    }
}
