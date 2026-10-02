namespace Vard.Abstractions
{
    /// <summary>
    /// Marcador semântico para políticas que implementam execução síncrona.
    /// Herda <see cref="IPolicy"/> — use <c>IPolicy</c> como tipo em geral;
    /// use <c>ISyncPolicy</c> quando precisar sinalizar explicitamente que a
    /// implementação é síncrona (sem async overhead).
    /// </summary>
    public interface ISyncPolicy : IPolicy { }
}
