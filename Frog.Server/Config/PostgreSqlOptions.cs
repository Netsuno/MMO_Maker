namespace Frog.Server.Config;

public sealed class PostgreSqlOptions
{
    public bool Enabled { get; set; }

    /// <summary>Autorise les dépôts en mémoire hors playtest (tests unitaires uniquement).</summary>
    public bool AllowInMemoryFallback { get; set; }

    public string? ConnectionString { get; set; }

    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("PostgreSql:ConnectionString requis quand Enabled=true.");
        }
    }
}
