namespace Frog.Server.Database;

/// <summary>Plafonds pour <c>character_payload_kv</c> (texte UTF-8, pas de type JSON SQL).</summary>
internal static class CharacterPayloadKvLimits
{
    /// <summary>Par valeur stockée (fragment JSON côté wire / client).</summary>
    public const int MaxEntryValueUtf8Bytes = 4 * 1024 * 1024;

    public const int MaxEntryKeyUtf8Bytes = 128;
}
