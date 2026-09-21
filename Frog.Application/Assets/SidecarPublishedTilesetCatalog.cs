using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Application.Assets;

/// <summary>
/// Catalogue tileset publié lu depuis un JSON additif (playtest : à côté du manifeste).
/// Livré au <see cref="PublishedCatalogWire"/> sans bump de protocole.
/// </summary>
public sealed class SidecarPublishedTilesetCatalog : IPublishedTilesetCatalog, IPublishedTilesetImageSource
{
    public const string FileName = "published-tilesets.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly string _path;

    public SidecarPublishedTilesetCatalog(string jsonPath)
    {
        _path = jsonPath ?? throw new ArgumentNullException(nameof(jsonPath));
    }

    public static string PathBesideManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new InvalidOperationException("Répertoire manifeste playtest introuvable.");
        }

        return Path.Combine(dir, FileName);
    }

    public static void Write(string jsonPath, IEnumerable<PublishedTilesetWireEntry> tilesets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        var dir = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var wire = new PublishedCatalogWire
        {
            Tilesets = tilesets.ToArray(),
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(wire, JsonOptions));
    }

    public static void WriteFromFiles(
        string jsonPath,
        IReadOnlyList<MapTilesetFile> files,
        IReadOnlyList<TilesetDefinition>? published = null)
    {
        var byPalette = (published ?? Array.Empty<TilesetDefinition>())
            .Where(d => d.EditorPaletteId is > 0)
            .GroupBy(d => d.EditorPaletteId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var entries = new List<PublishedTilesetWireEntry>();
        foreach (var file in files.Where(f => f.Id > 0 && f.PngBytes.Length > 0).OrderBy(f => f.Id))
        {
            byPalette.TryGetValue(file.Id, out var def);
            var sha = TilesetDefinition.ComputeSha256Hex(file.PngBytes);
            PngImageHeader.TryRead(file.PngBytes, out var w, out var h);
            entries.Add(new PublishedTilesetWireEntry
            {
                Id = def?.Id.ToString("D") ?? Guid.Empty.ToString("D"),
                Name = def?.Name ?? $"Tileset {file.Id}",
                PaletteId = file.Id,
                LogicalPath = def?.LogicalPath ?? $"tiles/{file.Id}.png",
                Sha256Hex = sha,
                TileSizePixels = def is { TileSizePixels: > 0 } ? def.TileSizePixels : 32,
                WidthPixels = w > 0 ? w : def?.WidthPixels ?? 0,
                HeightPixels = h > 0 ? h : def?.HeightPixels ?? 0,
                PngBase64 = Convert.ToBase64String(file.PngBytes),
            });
        }

        Write(jsonPath, entries);
    }

    public Task<IReadOnlyList<TilesetDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryRead(out var catalog) || catalog.Tilesets.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<TilesetDefinition>>(Array.Empty<TilesetDefinition>());
        }

        var list = new List<TilesetDefinition>();
        foreach (var entry in catalog.Tilesets)
        {
            if (!PublishedTilesetClientCoverage.TryDecodeCatalogPng(entry, out var paletteId, out var png))
            {
                continue;
            }

            _ = Guid.TryParse(entry.Id, out var id);
            list.Add(new TilesetDefinition
            {
                Id = id == Guid.Empty ? Guid.NewGuid() : id,
                Name = string.IsNullOrWhiteSpace(entry.Name) ? $"Tileset {paletteId}" : entry.Name,
                LogicalPath = string.IsNullOrWhiteSpace(entry.LogicalPath)
                    ? $"tiles/{paletteId}.png"
                    : entry.LogicalPath,
                TileSizePixels = entry.TileSizePixels > 0 ? entry.TileSizePixels : 32,
                WidthPixels = entry.WidthPixels,
                HeightPixels = entry.HeightPixels,
                Sha256Hex = string.IsNullOrWhiteSpace(entry.Sha256Hex)
                    ? TilesetDefinition.ComputeSha256Hex(png)
                    : entry.Sha256Hex,
                EditorPaletteId = paletteId,
                PngBytes = png,
            });
        }

        return Task.FromResult<IReadOnlyList<TilesetDefinition>>(list);
    }

    public bool TryReadPng(TilesetDefinition definition, out byte[] bytes)
        => EmbeddedPublishedTilesetImageSource.Instance.TryReadPng(definition, out bytes);

    public bool TryRead(out PublishedCatalogWire catalog)
    {
        catalog = new PublishedCatalogWire();
        if (!File.Exists(_path))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PublishedCatalogWire>(File.ReadAllText(_path), JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            catalog = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
