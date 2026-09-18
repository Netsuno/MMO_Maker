namespace Frog.Server.Security;

/// <summary>
/// <c>WorldFlagsPatchRequest</c> (opcode 34) is a leftover client-authored merge of
/// <c>payload.worldFlags</c>. Phase 8 disabled it in PostgreSQL production; Phase 9
/// also disables it in any production composition (not playtest, not in-memory fallback).
/// </summary>
public static class WorldFlagsPatchPolicy
{
    public const string RejectedMessage = "WorldFlagsPatch desactive en production PostgreSQL (Phase 8).";

    public static bool IsRejected(bool postgresEnabled, bool playtestEnabled, bool allowInMemoryFallback)
        => postgresEnabled || (!playtestEnabled && !allowInMemoryFallback);
}
