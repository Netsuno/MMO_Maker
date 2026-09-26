namespace Frog.Core.Maps;

/// <summary>
/// Libellés français du mode Région (1–63) et de la table de rencontres.
/// L’UI éditeur les reprend tels quels. Aucun asset VX.
/// </summary>
public static class MapRegionLabels
{
    public const string PanelTitle = "Régions";
    public const string RegionNumber = "Numéro";
    public const string Hint = "0 efface. 1–63 comme le mode Région. Le numéro s’affiche sur la carte.";
    public const string Encounters = "Rencontres";
    public const string Steps = "Pas moyens";
    public const string Monster = "Monstre";
    public const string FreeEntry = "Saisie libre";
    public const string Name = "Nom";
    public const string Identifier = "Identifiant";
    public const string Alias = "Alias";
    public const string Weight = "Poids";
    public const string EncounterRegions = "Régions";
    public const string WholeMapHint = "vide = toute la carte";
    public const string Add = "Ajouter";
    public const string Remove = "Retirer";
    public const string Apply = "Appliquer";
    public const string RefreshCatalog = "Actualiser";
    public const string NoCatalog = "Aucun monstre publié — saisissez un nom ou un identifiant.";
    public const string CatalogReady = "Monstres du catalogue. La saisie libre reste possible.";
    public const string ToolName = "Région";
    public const string EditMenu = "Outil région";
    public const string WholeMap = "toute la carte";

    public static string FormatStatus(byte regionId)
        => $"Région (G) · numéro {regionId} · clic peint · clic droit efface · glisser étend";
}
