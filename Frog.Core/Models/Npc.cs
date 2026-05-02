// #TODO (FR) : Définir le schéma NPC (stats, IA, tables de butin, cheminement, dialogues).
#nullable enable
namespace Frog.Core.Models;

/// <summary>Définition d’un PNJ (NPC) générique.</summary>
public sealed class Npc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // #TODO (FR) : Caractéristiques, comportement, zone d’apparition, respawn, faction, scripts.
}
