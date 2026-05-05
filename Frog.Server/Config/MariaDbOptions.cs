namespace Frog.Server.Config;

public sealed class MariaDbOptions
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
            throw new ArgumentException("MariaDb.ConnectionString est requis quand MariaDb.Enabled=true.");
        }
    }
}
