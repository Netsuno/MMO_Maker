// #TODO (FR) : Définir le schéma d’item (statistiques, effets, pile max, emplacements d’équipement).
#nullable enable
namespace Frog.Core.Models;

using Frog.Core.Enums;

/// <summary>Définition d’objet (Item) commune Client/Serveur/Éditeur.</summary>
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    // #TODO (FR) : Rareté, prix, poids, script d’utilisation, paramètres supplémentaires.
}
