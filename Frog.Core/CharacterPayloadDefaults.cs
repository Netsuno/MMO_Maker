namespace Frog.Core;

/// <summary>JSON minimal pour dev / perso legacy ; MariaDB persiste en tables + LONGTEXT KV (voir migrations v8–v10).</summary>
public static class CharacterPayloadDefaults
{
    /// <summary>Utilisé seulement si la colonne legacy <c>payload</c> existe encore (avant migration v10).</summary>
    public const string EmptyPayloadJson = "{}";

    /// <summary>Stats équilibrées par défaut (STR, AGI, DEX, INT, VIT, LUCK) pour le perso « Hero » initial.</summary>
    public const string NewHeroJson =
        "{\"stats\":{\"STR\":10,\"AGI\":10,\"DEX\":10,\"INT\":10,\"VIT\":10,\"LUCK\":10}}";
}
