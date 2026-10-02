using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Contrato de execução síncrona de política.
    /// </summary>
    public interface IPolicy
    {
        TResult Execute<TResult>(Func<TResult> action);
        TResult Execute<TResult>(Func<System.Collections.Generic.IDictionary<string, object>, TResult> action,
                                 System.Collections.Generic.IDictionary<string, object> context);

        PolicyResult<TResult> ExecuteAndCapture<TResult>(Func<TResult> action);
        PolicyResult<TResult> ExecuteAndCapture<TResult>(
            Func<System.Collections.Generic.IDictionary<string, object>, TResult> action,
            System.Collections.Generic.IDictionary<string, object> context);
    }
}
