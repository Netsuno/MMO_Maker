using System;
using System.IO;
using Frog.Application.Prefabs;
using Frog.Core.IO;
using Xunit;

namespace Frog.Tests;

/// <summary>Prefab MVP : placeholders CC0 in-repo + chemin de dessin client statique.</summary>
public sealed class ClientPrefabDrawTests
{
    [Fact]
    public void PlaceholderPngs_MatchExpectedIhdr()
    {
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "sofa-south.png"), 64, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "sofa-north.png"), 64, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "sofa-east.png"), 32, 64);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "sofa-west.png"), 32, 64);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "fence-post.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "fence-h.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "fence-v.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "bed-south.png"), 64, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "bed-north.png"), 64, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "bed-east.png"), 32, 64);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "bed-west.png"), 32, 64);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "table.png"), 64, 64);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "chair-south.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "plant.png"), 32, 32);
        AssertSheetPng(Path.Combine(RepoRoot(), "assets", "prefabs", "chest.png"), 32, 32);
    }

    [Fact]
    public void CatalogJson_MatchesBuiltInIds()
    {
        var path = Path.Combine(RepoRoot(), "assets", "prefabs", "catalog.json");
        var catalog = PrefabCatalogJson.TryDeserializeFromFile(path);
        Assert.NotNull(catalog);
        Assert.True(PrefabPlacementService.TryValidateCatalog(catalog!, out var error), error);
        Assert.Contains(catalog!.Prefabs, p => p.Id == BuiltInPrefabCatalog.SofaId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.FencePostId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.FenceRailId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.BedId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.TableId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.ChairId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.PlantId);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.ChestId);
    }

    [Fact]
    public void DrawPath_UsesExistingLoaders_NoProtocolBump()
    {
        var renderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var loader = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Assets", "ClientPrefabLoader.cs"));
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var generator = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "generate-prefab-placeholders.py"));
        var importer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Application", "Assets", "ProjectAssetKind.cs"));

        Assert.Contains("DrawPlacedPrefabs", renderer, StringComparison.Ordinal);
        Assert.Contains("prefabPlacements", renderer, StringComparison.Ordinal);
        Assert.Contains("ClientTilesetLoader", loader, StringComparison.Ordinal);
        Assert.Contains("ResolveSearchDirectories", loader, StringComparison.Ordinal);
        Assert.Contains("ClientPublishedPrefabMaterializer", shell, StringComparison.Ordinal);
        Assert.Contains("\"Maps\"", loader, StringComparison.Ordinal);
        Assert.Contains("FolderName", loader, StringComparison.Ordinal);
        Assert.Contains("ReloadPrefabOverlays", shell, StringComparison.Ordinal);
        Assert.Contains("prefabPlacements: _prefabPlacements", shell, StringComparison.Ordinal);
        Assert.Contains("Never embeds Graal", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("urllib", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urlopen", generator, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prefabs", importer, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsPrefabMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "prefabs", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pngBase64", text, StringComparison.Ordinal);
        Assert.Contains("prefabMaps", text, StringComparison.Ordinal);
        Assert.Contains("ProjectAssetImporter", text, StringComparison.Ordinal);
        Assert.Contains(".prefabs.json", text, StringComparison.Ordinal);
        Assert.Contains("MapCanvas", text, StringComparison.Ordinal);
        Assert.Contains("MapViewRenderer", text, StringComparison.Ordinal);
        Assert.Contains("CC0", text, StringComparison.Ordinal);
        Assert.Contains("pipette", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Graal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("reste 11", text, StringComparison.Ordinal);
    }

    private static void AssertSheetPng(string path, int width, int height)
    {
        Assert.True(File.Exists(path), path);
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 33, "PNG too small: " + path);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal(width, ReadBigEndianInt32(bytes, 16));
        Assert.Equal(height, ReadBigEndianInt32(bytes, 20));
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | (bytes[offset + 3]);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
