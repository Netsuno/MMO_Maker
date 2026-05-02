namespace Frog.Server.Config;

public sealed class PostgresOptions
{
    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new ArgumentException("Postgres.ConnectionString est requis quand Postgres.Enabled=true.");
        }
    }
}
