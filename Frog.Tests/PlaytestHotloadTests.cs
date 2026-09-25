using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class PlaytestHotloadTests
{
    [Fact]
    public void HelloProtocol_StaysAt11()
    {
        Assert.Equal(11, FrogWireProtocol.Version);
    }

    [Fact]
    public void Paths_SiblingPublishLayouts_WinOverRepoBin()
    {
        // Quatre niveaux au-dessus de l’éditeur doivent rester sous le dossier temporaire
        // (le repli bin/ du dépôt est relatif en ../../../../).
        var scratch = Path.Combine(Path.GetTempPath(), "frog-playtest-layouts-" + Guid.NewGuid().ToString("N"));
        var publishRoot = Path.Combine(scratch, "a", "b", "c", "d");
        var editorDir = Path.Combine(publishRoot, PlaytestPublishLayouts.EditorWinFolder);
        var clientDir = Path.Combine(publishRoot, PlaytestPublishLayouts.ClientWinFolder);
        var serverDir = Path.Combine(publishRoot, PlaytestPublishLayouts.ServerWinFolder);
        var binClient = Path.GetFullPath(Path.Combine(
            editorDir, "..", "..", "..", "..", "Frog.Client", "bin", "Debug", "net8.0-windows",
            PlaytestPublishLayouts.ClientExeFileName));
        var binServer = Path.GetFullPath(Path.Combine(
            editorDir, "..", "..", "..", "..", "Frog.Server", "bin", "Debug", "net8.0",
            PlaytestPublishLayouts.ServerExeFileName));
        Directory.CreateDirectory(editorDir);
        Directory.CreateDirectory(clientDir);
        Directory.CreateDirectory(serverDir);
        Directory.CreateDirectory(Path.GetDirectoryName(binClient)!);
        Directory.CreateDirectory(Path.GetDirectoryName(binServer)!);
        var siblingClient = Path.Combine(clientDir, PlaytestPublishLayouts.ClientExeFileName);
        var siblingServer = Path.Combine(serverDir, PlaytestPublishLayouts.ServerExeFileName);
        File.WriteAllBytes(siblingClient, [1]);
        File.WriteAllBytes(siblingServer, [1]);
        File.WriteAllBytes(binClient, [2]);
        File.WriteAllBytes(binServer, [2]);
        try
        {
            var clientCandidates = PlaytestPublishLayouts.EnumerateClientCandidates(editorDir).ToArray();
            Assert.Contains(
                clientCandidates,
                path => path.Replace('\\', '/').Contains("client-win-x64/Frog.Client.exe", StringComparison.Ordinal));
            var serverCandidates = PlaytestPublishLayouts.EnumerateServerCandidates(editorDir).Select(c => c.Path).ToArray();
            Assert.Contains(
                serverCandidates,
                path => path.Replace('\\', '/').Contains("server-win-x64/Frog.Server.exe", StringComparison.Ordinal));

            Assert.True(PlaytestPublishLayouts.TryResolvePair(
                editorDir,
                reuseClientExe: null,
                reuseServerExe: null,
                out var clientExe,
                out var serverExe,
                out var useDll));
            Assert.False(useDll);
            Assert.Equal(Path.GetFullPath(siblingClient), clientExe);
            Assert.Equal(Path.GetFullPath(siblingServer), serverExe);
        }
        finally
        {
            TryDelete(scratch);
        }
    }

    [Fact]
    public void Paths_ReusesPreviousBinaries_WhenTheyStillExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "frog-playtest-reuse-" + Guid.NewGuid().ToString("N"));
        var editorDir = Path.Combine(root, PlaytestPublishLayouts.EditorWinFolder);
        var siblingClientDir = Path.Combine(root, PlaytestPublishLayouts.ClientWinFolder);
        var siblingServerDir = Path.Combine(root, PlaytestPublishLayouts.ServerWinFolder);
        var reusedDir = Path.Combine(root, "reused");
        Directory.CreateDirectory(editorDir);
        Directory.CreateDirectory(siblingClientDir);
        Directory.CreateDirectory(siblingServerDir);
        Directory.CreateDirectory(reusedDir);
        File.WriteAllBytes(Path.Combine(siblingClientDir, PlaytestPublishLayouts.ClientExeFileName), [1]);
        File.WriteAllBytes(Path.Combine(siblingServerDir, PlaytestPublishLayouts.ServerExeFileName), [1]);
        var reusedClient = Path.Combine(reusedDir, PlaytestPublishLayouts.ClientExeFileName);
        var reusedServer = Path.Combine(reusedDir, PlaytestPublishLayouts.ServerExeFileName);
        File.WriteAllBytes(reusedClient, [3]);
        File.WriteAllBytes(reusedServer, [3]);
        try
        {
            Assert.True(PlaytestPublishLayouts.TryResolvePair(
                editorDir,
                reusedClient,
                reusedServer,
                out var clientExe,
                out var serverExe,
                out _));
            Assert.Equal(Path.GetFullPath(reusedClient), clientExe);
            Assert.Equal(Path.GetFullPath(reusedServer), serverExe);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void SaveBeforePlay_CancelLeavesDecisionWithoutLaunch()
    {
        var map = OpenMap("Prairie", 8, 8);
        var decision = PlaytestHotload.Decide(new PlaytestHotloadRequest
        {
            IsDirty = true,
            SaveChoice = PlaytestSaveChoice.Cancel,
            CurrentMapId = Guid.NewGuid(),
            Map = map,
            RememberedTileX = 2,
            RememberedTileY = 3,
        });

        var cancelled = Assert.IsType<PlaytestHotloadDecision.Cancelled>(decision);
        Assert.Equal(PlaytestHotload.CancelledDirtyMessage, cancelled.Reason);
    }

    [Fact]
    public void Spawn_UsesOpenMapAndRememberedTile_NotDemoMap()
    {
        var openMapId = Guid.Parse("22222222-2222-2222-2222-222222222202");
        var map = OpenMap("Forêt chaude", 8, 8);
        var decision = PlaytestHotload.Decide(new PlaytestHotloadRequest
        {
            IsDirty = true,
            SaveChoice = PlaytestSaveChoice.Save,
            CurrentMapId = openMapId,
            Map = map,
            RememberedTileX = 4,
            RememberedTileY = 5,
            FallbackTileX = 0,
            FallbackTileY = 0,
        });

        var ready = Assert.IsType<PlaytestHotloadDecision.Ready>(decision);
        Assert.True(ready.SaveBeforeLaunch);
        Assert.True(ready.UsedRememberedSpawn);
        Assert.Equal(openMapId, ready.CanonicalMapId);
        Assert.NotEqual(DemoMapFactory.DefaultMapId, ready.CanonicalMapId);
        Assert.Equal(4, ready.TileX);
        Assert.Equal(5, ready.TileY);
    }

    [Fact]
    public async Task SaveBeforePlay_DirtyTileIsInNextPublishedSnapshot()
    {
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        var map = OpenMap("Clairière", 6, 6);
        workspace.AdoptLocalDraft(map);
        Assert.NotEqual(DemoMapFactory.DefaultMapId, workspace.CurrentMapId ?? Guid.Empty);
        Assert.True(workspace.IsDirty);

        var decision = PlaytestHotload.Decide(new PlaytestHotloadRequest
        {
            IsDirty = workspace.IsDirty,
            SaveChoice = PlaytestSaveChoice.Save,
            CurrentMapId = workspace.CurrentMapId,
            Map = workspace.CurrentMap!,
            RememberedTileX = 1,
            RememberedTileY = 2,
        });
        var ready = Assert.IsType<PlaytestHotloadDecision.Ready>(decision);
        Assert.True(ready.SaveBeforeLaunch);

        var preparer = new PlaytestMapPreparer(repo);
        var first = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
                SpawnTileX = ready.TileX,
                SpawnTileY = ready.TileY,
                Port = 6401,
            });
        var firstSuccess = Assert.IsType<PlaytestPreparationResult.Success>(first);
        Assert.NotEqual(DemoMapFactory.DefaultMapId, firstSuccess.Plan.PrimaryCanonicalMapId);
        Assert.Equal(workspace.CurrentMapId, firstSuccess.Plan.PrimaryCanonicalMapId);
        Assert.Equal(ready.TileX, firstSuccess.Plan.Spawn.TileX);
        Assert.Equal(ready.TileY, firstSuccess.Plan.Spawn.TileY);
        Assert.Equal(firstSuccess.Plan.Spawn.RuntimeMapId, firstSuccess.Plan.Maps[0].RuntimeMapId);
        Assert.False(workspace.IsDirty);

        var edited = workspace.CurrentMap!.Layers[0].Tiles.Single(t => t.X == 3 && t.Y == 1);
        edited.Type = TileType.Block;
        edited.TilesetId = 9;
        workspace.MarkDirty();

        var secondDecision = PlaytestHotload.Decide(new PlaytestHotloadRequest
        {
            IsDirty = true,
            SaveChoice = PlaytestSaveChoice.Save,
            CurrentMapId = workspace.CurrentMapId,
            Map = workspace.CurrentMap,
            RememberedTileX = firstSuccess.Plan.Spawn.TileX,
            RememberedTileY = firstSuccess.Plan.Spawn.TileY,
        });
        Assert.IsType<PlaytestHotloadDecision.Ready>(secondDecision);

        var second = await preparer.PrepareAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
                SpawnTileX = firstSuccess.Plan.Spawn.TileX,
                SpawnTileY = firstSuccess.Plan.Spawn.TileY,
                Port = 6402,
            });
        var secondSuccess = Assert.IsType<PlaytestPreparationResult.Success>(second);
        Assert.Equal(firstSuccess.Plan.PrimaryCanonicalMapId, secondSuccess.Plan.PrimaryCanonicalMapId);
        Assert.True(secondSuccess.Plan.PrimaryPublishedRevision > firstSuccess.Plan.PrimaryPublishedRevision);

        var fmapPath = Path.Combine(
            secondSuccess.Plan.WorkDirectory,
            $"map-{secondSuccess.Plan.Spawn.RuntimeMapId}.fmap");
        var reloaded = new MapSerializer().Deserialize(File.ReadAllBytes(fmapPath));
        var block = reloaded.Layers[0].Tiles.Single(t => t.X == 3 && t.Y == 1);
        Assert.Equal(TileType.Block, block.Type);
        Assert.Equal(9, block.TilesetId);
        Assert.Equal(1, secondSuccess.Plan.Spawn.RuntimeMapId);
    }

    [Fact]
    public void PlacedEntities_NextSessionReadsMovedNpcAndObject()
    {
        var root = Path.Combine(Path.GetTempPath(), "frog-playtest-placed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var map = OpenMap("Hameau", 8, 8);
            var mapId = Guid.NewGuid();
            var npc = MapPlacedEntityEdit.Create(MapPlacedKind.Npc, 1, 1, 1);
            npc.Name = "Garde";
            npc.Level = 4;
            var obj = MapPlacedEntityEdit.Create(MapPlacedKind.Object, 2, 2, 1);
            obj.Name = "Coffre";
            PlaytestPlacedEntityPackage.Write(root, runtimeMapId: 1, mapId, map.Name, map, new[] { npc, obj });

            var first = PlaytestPlacedEntityPackage.TryLoadForRuntimeMap(new[] { root }, 1, map);
            Assert.Equal(2, first.Count);
            Assert.Contains(first, e => e.Kind == MapPlacedKind.Npc && e.TileX == 1 && e.Name == "Garde");

            npc.TileX = 5;
            npc.TileY = 6;
            obj.TileX = 3;
            obj.TileY = 3;
            var spawn = MapPlacedEntityEdit.Create(MapPlacedKind.Spawn, 0, 4, 1);
            spawn.Name = "Camp";
            PlaytestPlacedEntityPackage.Write(root, runtimeMapId: 1, mapId, map.Name, map, new[] { npc, obj, spawn });

            var second = PlaytestPlacedEntityPackage.TryLoadForRuntimeMap(new[] { root }, 1, map);
            Assert.Equal(3, second.Count);
            var moved = Assert.Single(second, e => e.Id == npc.Id);
            Assert.Equal(5, moved.TileX);
            Assert.Equal(6, moved.TileY);
            Assert.Equal(4, moved.Level);
            Assert.Contains(second, e => e.Kind == MapPlacedKind.Object && e.TileX == 3 && e.TileY == 3);
            Assert.Contains(second, e => e.Kind == MapPlacedKind.Spawn && e.Name == "Camp");
            Assert.Empty(PlaytestPlacedEntityPackage.TryLoadForRuntimeMap(new[] { root }, 2, map));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static Map OpenMap(string name, int w, int h)
    {
        var map = new Map { Name = name, Width = w, Height = h };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                ground.Tiles.Add(new Tile { X = x, Y = y, TilesetId = 1, Type = TileType.Ground });
            }
        }

        map.Layers.Add(ground);
        return map;
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // temp
        }
    }
}
