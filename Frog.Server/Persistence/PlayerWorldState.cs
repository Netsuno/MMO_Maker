namespace Frog.Server.Persistence;

public readonly record struct PlayerWorldState(int MapId, int X, int Y, string? CharacterId = null);
