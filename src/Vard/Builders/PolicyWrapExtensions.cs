using System;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    /// <summary>
    /// Métodos de extensão para composição de políticas via PolicyWrap.
    /// </summary>
    public static class PolicyWrapExtensions
    {
        /// <summary>
        /// Encadeia duas políticas de resiliência. OuterPolicy executa por fora encapsulando InnerPolicy.
        /// </summary>
        public static PolicyWrap Wrap(this IPolicy outerPolicy, IPolicy innerPolicy)
        {
            if (outerPolicy == null) throw new ArgumentNullException(nameof(outerPolicy));
            if (innerPolicy == null) throw new ArgumentNullException(nameof(innerPolicy));
            return new PolicyWrap(outerPolicy, innerPolicy);
        }

        /// <summary>
        /// Encadeia múltiplas políticas de resiliência.
        /// A primeira política do array é a mais externa e a última é a mais interna.
        /// Exemplo: Wrap(p1, p2, p3) equivale a p1.Wrap(p2.Wrap(p3)).
        /// </summary>
        public static PolicyWrap Wrap(params IPolicy[] policies)
        {
            if (policies == null) throw new ArgumentNullException(nameof(policies));
            if (policies.Length < 2)
                throw new ArgumentException("At least two policies are required to create a PolicyWrap.", nameof(policies));

            PolicyWrap wrap = new PolicyWrap(policies[policies.Length - 2], policies[policies.Length - 1]);
            for (int i = policies.Length - 3; i >= 0; i--)
            {
                wrap = new PolicyWrap(policies[i], wrap);
            }
            return wrap;
        }
    }
}
