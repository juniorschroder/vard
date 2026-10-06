using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão fluentes para configuração da política de tempo limite (<see cref="TimeoutPolicy"/>).
    /// </summary>
    public static class TimeoutPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de timeout estrito com callbacks opcionais em caso de estouro.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="timeout">A duração máxima de tempo limite permitida para cada execução.</param>
        /// <param name="onTimeout">Callback síncrono disparado quando ocorre timeout.</param>
        /// <param name="onTimeoutAsync">Callback assíncrono disparado quando ocorre timeout.</param>
        /// <returns>Uma nova instância de <see cref="TimeoutPolicy"/>.</returns>
        public static TimeoutPolicy Timeout(
            this IPolicyBuilder builder,
            TimeSpan timeout,
            Action<IDictionary<string, object>?, TimeSpan>? onTimeout = null,
            Func<IDictionary<string, object>?, TimeSpan, Task>? onTimeoutAsync = null)
        {
            return new TimeoutPolicy(timeout, onTimeout, onTimeoutAsync);
        }
    }
}
