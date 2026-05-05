namespace Frog.Core;

/// <summary>Schémas JSON minimaux persistés (<c>frog_character.payload</c>) ; évolutions versionnées côté serveur.</summary>
public static class CharacterPayloadDefaults
{
    /// <summary>Stats équilibrées par défaut (STR, AGI, DEX, INT, VIT, LUCK) pour le perso « Hero » initial.</summary>
    public const string NewHeroJson =
        "{\"stats\":{\"STR\":10,\"AGI\":10,\"DEX\":10,\"INT\":10,\"VIT\":10,\"LUCK\":10}}";
}
