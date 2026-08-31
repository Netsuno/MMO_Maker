namespace Frog.Application.Maps;

/// <summary>Indique si le dépôt courant permet une persistance durable (PostgreSQL).</summary>
public sealed class MapRepositoryCapabilities
{
    public required bool IsDurablePersistence { get; init; }

    /// <summary>Autorise Save/Publish vers le dépôt courant (PostgreSQL ou mémoire test).</summary>
    public required bool AllowsSave { get; init; }

    /// <summary>Libellé UI : jamais « PostgreSQL » si <see cref="IsDurablePersistence"/> est faux.</summary>
    public required string DisplayLabel { get; init; }

    public static MapRepositoryCapabilities PostgreSql { get; } = new()
    {
        IsDurablePersistence = true,
        AllowsSave = true,
        DisplayLabel = "PostgreSQL",
    };

    public static MapRepositoryCapabilities InMemoryDemo { get; } = new()
    {
        IsDurablePersistence = false,
        AllowsSave = false,
        DisplayLabel = "mémoire (démo — non persistant)",
    };

    public static MapRepositoryCapabilities InMemoryTest { get; } = new()
    {
        IsDurablePersistence = false,
        AllowsSave = true,
        DisplayLabel = "mémoire (test)",
    };
}
