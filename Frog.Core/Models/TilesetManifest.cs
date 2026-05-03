#nullable enable
using System.Collections.Generic;

namespace Frog.Core.Models;

/// <summary>
/// Liste des tilesets référencés par une carte, exportée à côté du <c>.fmap</c> (même nom de base + <c>.tilesets.json</c>).
/// Le client charge les PNG depuis le dossier du manifest (chemins relatifs = noms de fichier dans <see cref="FileName"/>).
/// </summary>
public sealed class TilesetManifest
{
    public int ManifestVersion { get; set; } = 1;

    public List<TilesetManifestEntry> Entries { get; set; } = new();
}

public sealed class TilesetManifestEntry
{
    public int Id { get; set; }

    /// <summary>Nom de fichier seul (ex. <c>grass.png</c>), résolu dans le même dossier que le fichier manifest.</summary>
    public string FileName { get; set; } = string.Empty;
}
