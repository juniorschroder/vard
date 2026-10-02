using System;
using System.Collections.Generic;

namespace Vard.Builders
{
    /// <summary>
    /// Armazena os predicados de tratamento configurados pelo caller.
    /// A semântica OR é implícita: qualquer predicado que retorne true
    /// dispara a política.
    /// </summary>
    internal sealed class HandleCondition
    {
        private readonly List<Func<object?, bool>> _predicates = new List<Func<object?, bool>>();

        /// <summary>
        /// Adiciona um predicado baseado em tipo de exceção.
        /// </summary>
        internal void AddExceptionPredicate<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception
        {
            _predicates.Add(obj =>
            {
                if (obj is TException ex)
                    return predicate == null || predicate(ex);
                return false;
            });
        }

        /// <summary>
        /// Adiciona um predicado baseado em resultado (D-11: boxing trick).
        /// </summary>
        internal void AddResultPredicate<TResult>(Func<TResult, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            // Boxing: armazena como Func<object?, bool> compatível com ShouldHandle
            _predicates.Add(obj => obj is TResult result && predicate(result));
        }

        /// <summary>
        /// Avalia se a exceção ou resultado deve ser tratado pela política (D-12).
        /// Retorna true se qualquer predicado retornar true.
        /// </summary>
        internal bool ShouldHandle(Exception? exception, object? result)
        {
            object? subject = (object?)exception ?? result;
            foreach (var predicate in _predicates)
            {
                if (predicate(subject))
                    return true;
            }
            return false;
        }
    }
}
