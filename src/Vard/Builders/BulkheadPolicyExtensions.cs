using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para configuração de política de Bulkhead.
    /// </summary>
    public static class BulkheadPolicyExtensions
    {
        /// <summary>
        /// Configura uma política de Bulkhead para isolamento de recursos e contenção de concorrência.
        /// </summary>
        /// <param name="builder">O builder de política fluente.</param>
        /// <param name="maxParallelization">Número máximo de execuções concorrentes admitidas.</param>
        /// <param name="maxQueuedActions">Número máximo de chamadas enfileiradas aguardando slot disponível.</param>
        /// <param name="onBulkheadRejected">Callback síncrono opcional disparado em caso de rejeição por capacidade esgotada.</param>
        /// <param name="onBulkheadRejectedAsync">Callback assíncrono opcional disparado em caso de rejeição por capacidade esgotada.</param>
        /// <returns>Uma nova instância de <see cref="BulkheadPolicy"/>.</returns>
        public static BulkheadPolicy Bulkhead(
            this IPolicyBuilder builder,
            int maxParallelization,
            int maxQueuedActions = 0,
            Action<IDictionary<string, object>?>? onBulkheadRejected = null,
            Func<IDictionary<string, object>?, Task>? onBulkheadRejectedAsync = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return new BulkheadPolicy(maxParallelization, maxQueuedActions, onBulkheadRejected, onBulkheadRejectedAsync);
        }
    }
}
