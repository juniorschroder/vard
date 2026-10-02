using System;
using Vard.Abstractions;

namespace Vard.Builders
{
    /// <summary>
    /// Implementação interna do builder fluente de políticas.
    /// Não exposto ao consumidor NuGet (D-15).
    /// </summary>
    internal sealed class PolicyBuilder : IPolicyBuilder
    {
        internal readonly HandleCondition Condition = new HandleCondition();

        public IPolicyBuilder Handle<TException>() where TException : Exception
        {
            Condition.AddExceptionPredicate<TException>();
            return this;
        }

        public IPolicyBuilder Handle<TException>(Func<TException, bool> predicate) where TException : Exception
        {
            Condition.AddExceptionPredicate(predicate);
            return this;
        }

        public IPolicyBuilder HandleResult<TResult>(Func<TResult, bool> predicate)
        {
            Condition.AddResultPredicate(predicate);
            return this;
        }

        // D-11: Or() é alias de Handle()
        public IPolicyBuilder Or<TException>() where TException : Exception
            => Handle<TException>();

        public IPolicyBuilder Or<TException>(Func<TException, bool> predicate) where TException : Exception
            => Handle(predicate);

        public IPolicy Build()
        {
            // Fase 1: retorna PolicyWrap como IPolicy placeholder.
            // As políticas concretas (Retry, Timeout, etc.) são implementadas nas Fases 2–5.
            return new Policies.PolicyWrap(Condition);
        }
    }
}
