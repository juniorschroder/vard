using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato de execução assíncrona de política.
    /// </summary>
    public interface IAsyncPolicy
    {
        Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default);

        Task<PolicyResult<TResult>> ExecuteAndCaptureAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default);
    }
}
