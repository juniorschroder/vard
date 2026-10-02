using System;
using Vard.Abstractions;

namespace Vard.Builders
{
    /// <summary>
    /// Ponto de entrada estático da API Vard (D-06).
    /// Fases 2–5 adicionarão: VardPolicy.Retry(n), VardPolicy.Timeout(t), etc.
    /// </summary>
    public static class VardPolicy
    {
        /// <summary>
        /// Inicia a configuração de uma política tratando exceções do tipo
        /// <typeparamref name="TException"/> (D-07: pode ser chamado antes ou depois da política).
        /// </summary>
        public static IPolicyBuilder Handle<TException>() where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle<TException>();
        }

        /// <summary>
        /// Inicia a configuração de uma política tratando exceções do tipo
        /// <typeparamref name="TException"/> que satisfazem o predicado.
        /// </summary>
        public static IPolicyBuilder Handle<TException>(Func<TException, bool> predicate) where TException : Exception
        {
            var builder = new PolicyBuilder();
            return builder.Handle(predicate);
        }
    }
}
