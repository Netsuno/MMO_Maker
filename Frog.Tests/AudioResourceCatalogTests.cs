using System;
using System.IO;
using System.Linq;
using Frog.Application.Assets;
using Frog.Core.Constants;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class AudioResourceCatalogTests
{
    [Fact]
    public void List_SeparatesBgmAndSe_AndKeepsRelativeStoredPaths()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var root = NewRoot();
        try
        {
            Touch(root, "Audio/BGM/titre.ogg");
            Touch(root, "Audio/BGM/dungeon/boss.wav");
            Touch(root, "Audio/BGM/readme.txt");
            Touch(root, "Audio/BGM/.secret.wav");
            Touch(root, "Audio/SE/pas.wav");
            Touch(root, "Assets/Audio/fanfare.wav");
            Touch(root, "Assets/Audio/music-loop.wav");
            Touch(root, "Assets/Audio/BGM/ville.ogg");
            Touch(root, "Assets/Audio/SE/porte.wav");
            Touch(root, "Frog.Client/Assets/Audio/music-loop.wav");
            Touch(root, "Frog.Client/Assets/Audio/ui-click.wav");
            Touch(root, "Frog.Client/Assets/Audio/BGM/intro.mid");
            Touch(root, "Frog.Client/Assets/Audio/SE/click.flac");

            var bgm = AudioResourceCatalog.List(AudioResourceKind.Bgm, [root]);
            var se = AudioResourceCatalog.List(AudioResourceKind.Se, [root]);

            Assert.Equal(
            [
                "Assets/Audio/BGM/intro.mid",
                "Assets/Audio/BGM/ville.ogg",
                "Assets/Audio/fanfare.wav",
                "Assets/Audio/music-loop.wav",
                "Assets/Audio/ui-click.wav",
                "Audio/BGM/dungeon/boss.wav",
                "Audio/BGM/titre.ogg",
            ],
            bgm.Select(entry => entry.StoredAsset).ToArray());
            Assert.Equal(
            [
                "Assets/Audio/fanfare.wav",
                "Assets/Audio/music-loop.wav",
                "Assets/Audio/SE/click.flac",
                "Assets/Audio/SE/porte.wav",
                "Assets/Audio/ui-click.wav",
                "Audio/SE/pas.wav",
            ],
            se.Select(entry => entry.StoredAsset).ToArray());

            Assert.Single(bgm, entry => entry.StoredAsset == "Assets/Audio/music-loop.wav");
            foreach (var entry in bgm.Concat(se))
            {
                Assert.True(MapAudioTrack.TryNormalizeAsset(entry.StoredAsset, out var normalized, out var error), error);
                Assert.Equal(entry.StoredAsset, normalized);
                Assert.False(Path.IsPathRooted(entry.StoredAsset));
                Assert.DoesNotContain("..", entry.StoredAsset, StringComparison.Ordinal);
                Assert.True(File.Exists(entry.AbsolutePath));
            }
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void DiscoverSearchRoots_AddsProjectParent_WhenAssetRootIsAssets()
    {
        var scratch = NewRoot();
        var project = Path.Combine(scratch, "MyGame");
        var assets = Path.Combine(project, "Assets");
        var repo = Path.Combine(scratch, "Repo");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(repo);
        Touch(project, "Audio/BGM/theme.ogg");
        Touch(assets, "Audio/flat.wav");
        try
        {
            var roots = AudioResourceCatalog.DiscoverSearchRoots(assets, repo);
            Assert.Equal(
            [
                Path.GetFullPath(repo),
                Path.GetFullPath(assets),
                Path.GetFullPath(project),
            ],
            roots);

            var listed = AudioResourceCatalog.List(AudioResourceKind.Bgm, AudioResourceCatalog.DiscoverSearchRoots(assets, null));
            Assert.Contains(listed, entry => entry.StoredAsset == "Audio/BGM/theme.ogg");
            Assert.Contains(listed, entry => entry.StoredAsset == "Assets/Audio/flat.wav");

            var plain = Path.Combine(scratch, "PlainRoot");
            Directory.CreateDirectory(plain);
            Assert.Equal([Path.GetFullPath(plain)], AudioResourceCatalog.DiscoverSearchRoots(plain, null));
        }
        finally
        {
            Delete(scratch);
        }
    }

    [Fact]
    public void TryStoredAsset_OutsideRoots_KeepsFileNameOnly()
    {
        var root = NewRoot();
        var outside = Path.Combine(Path.GetTempPath(), "frog-audio-out-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(outside, [9]);
        try
        {
            Assert.True(AudioResourceCatalog.TryStoredAsset(outside, [root], out var name, out var error), error);
            Assert.Equal(Path.GetFileName(outside), name);
            Assert.False(Path.IsPathRooted(name));
            Assert.Empty(AudioResourceCatalog.List(AudioResourceKind.Bgm, [root]));
        }
        finally
        {
            Delete(root);
            if (File.Exists(outside))
            {
                File.Delete(outside);
            }
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "frog-audio-cat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Touch(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3]);
    }

    private static void Delete(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Le nettoyage ne doit pas masquer l’échec du test.
        }
    }
}
