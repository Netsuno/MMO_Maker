namespace Frog.Server.Config;

public sealed class SessionOptions
{
    public int IdleTimeoutSeconds { get; init; } = 300;
    public int CleanupIntervalSeconds { get; init; } = 30;

    public void Validate()
    {
        if (IdleTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IdleTimeoutSeconds), "IdleTimeoutSeconds doit etre > 0.");
        }

        if (CleanupIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CleanupIntervalSeconds), "CleanupIntervalSeconds doit etre > 0.");
        }
    }
}
