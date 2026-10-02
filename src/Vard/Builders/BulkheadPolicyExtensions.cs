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
