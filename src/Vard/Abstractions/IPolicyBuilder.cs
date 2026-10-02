using System;

namespace Vard.Abstractions
{
    /// <summary>
    /// Builder fluente para configuração de políticas.
    /// Fases 2–5 estendem via extension methods — não via herança (D-09).
    /// </summary>
    public interface IPolicyBuilder
    {
        /// <summary>Trata exceções do tipo <typeparamref name="TException"/>.</summary>
        IPolicyBuilder Handle<TException>() where TException : Exception;

        /// <summary>Trata exceções do tipo <typeparamref name="TException"/> que satisfazem o predicado.</summary>
        IPolicyBuilder Handle<TException>(Func<TException, bool> predicate) where TException : Exception;

        /// <summary>Trata resultados do tipo <typeparamref name="TResult"/> que satisfazem o predicado.</summary>
        IPolicyBuilder HandleResult<TResult>(Func<TResult, bool> predicate);

        /// <summary>Alias de Handle — adiciona mais uma condição OR (D-11).</summary>
        IPolicyBuilder Or<TException>() where TException : Exception;

        /// <summary>Alias de Handle com predicado — adiciona mais uma condição OR.</summary>
        IPolicyBuilder Or<TException>(Func<TException, bool> predicate) where TException : Exception;

        /// <summary>Constrói e retorna a política configurada.</summary>
        IPolicy Build();
    }
}
