namespace Vard.Abstractions
{
    /// <summary>
    /// Tipo sentinel para operações void. Usado como <c>PolicyResult&lt;Void&gt;</c>
    /// para evitar sobrecarga não-genérica da API.
    /// </summary>
    public struct Void
    {
        /// <summary>Instância singleton do tipo sentinel.</summary>
        public static readonly Void Instance = default;
    }
}
