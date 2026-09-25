using System;
using System.IO;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>Attack + death sheets: same 4-dir grid as walk, no protocol bump.</summary>
public sealed class ClientActionAnimTests
{
    [Fact]
    public void Protocol_Stays11_AndTilesStay32()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(32, WalkClock.NativeCellPixels);
        Assert.Equal(3, WalkClock.SheetColumns);
        Assert.Equal(4, WalkClock.SheetRows);
    }

    [Fact]
    public void AttackClock_PlaysThreeFramesThenFinishes_DeathHoldsLast()
    {
        Assert.Equal(0, ActionClock.Column(SpriteAction.Attack, 0));
        Assert.Equal(0, ActionClock.Column(SpriteAction.Attack, 89));
        Assert.Equal(1, ActionClock.Column(SpriteAction.Attack, 90));
        Assert.Equal(2, ActionClock.Column(SpriteAction.Attack, 180));
        Assert.Equal(2, ActionClock.Column(SpriteAction.Attack, 269));
        Assert.False(ActionClock.AttackFinished(ActionClock.AttackDurationMs - 1));
        Assert.True(ActionClock.AttackFinished(ActionClock.AttackDurationMs));

        Assert.Equal(0, ActionClock.Column(SpriteAction.Death, 0));
        Assert.Equal(1, ActionClock.Column(SpriteAction.Death, 140));
        Assert.Equal(2, ActionClock.Column(SpriteAction.Death, 280));
        Assert.Equal(2, ActionClock.Column(SpriteAction.Death, 10_000));
        Assert.False(ActionClock.DeathSettled(279));
        Assert.True(ActionClock.DeathSettled(ActionClock.DeathFrameMs * ActionClock.DeathFrames));
    }

    [Theory]
    [InlineData(Direction.Down, 0)]
    [InlineData(Direction.Left, 1)]
    [InlineData(Direction.Right, 2)]
    [InlineData(Direction.Up, 3)]
    public void Poses_UseFacingRow_AndActionColumn(Direction facing, int row)
    {
        var walk = new PlayerSpritePose(facing, Walking: true, ElapsedMs: 280);
        Assert.Equal(SpriteAction.Walk, walk.Action);
        Assert.Equal(2, walk.SheetColumn);
        Assert.Equal(row, walk.SheetRow);
        Assert.Equal(1, PlayerSpritePose.IdleDown.SheetColumn);

        var attack = new PlayerSpritePose(facing, Walking: false, Action: SpriteAction.Attack, ActionElapsedMs: 90);
        Assert.Equal(1, attack.SheetColumn);
        Assert.Equal(row, attack.SheetRow);

        var death = new PlayerSpritePose(facing, Walking: true, ElapsedMs: 9999, Action: SpriteAction.Death, ActionElapsedMs: 10_000);
        Assert.Equal(2, death.SheetColumn);
        Assert.Equal(row, death.SheetRow);

        var npc = new WorldSpritePose(facing, Walking: false, Action: SpriteAction.Attack, ActionElapsedMs: 0);
        Assert.Equal(0, npc.SheetColumn);
        Assert.Equal(row, npc.SheetRow);
        Assert.Equal(1, WorldSpritePose.IdleDown.SheetColumn);
    }

    [Fact]
    public void IncomingAttack_MatchesDefenderMeleeCopy()
    {
        Assert.True(ActionClock.IsIncomingAttack("Subi une attaque melee."));
        Assert.True(ActionClock.IsIncomingAttack("Subi une attaque à distance."));
        Assert.False(ActionClock.IsIncomingAttack("Hors portee."));
        Assert.False(ActionClock.IsIncomingAttack("Personnage mort."));
        Assert.False(ActionClock.IsIncomingAttack(null));
    }

    [Fact]
    public void ActionSheets_AreNinetySixByOneTwentyEight_AndDifferFromWalk()
    {
        var world = Path.Combine(RepoRoot(), "Frog.Client", "Assets", "World");
        AssertDiffers(world, "player-walk.png", "player-attack.png");
        AssertDiffers(world, "player-walk.png", "player-death.png");
        AssertDiffers(world, "player-attack.png", "player-death.png");
        AssertDiffers(world, "player-walk-body.png", "player-attack-body.png");
        AssertDiffers(world, "player-walk-head.png", "player-death-head.png");
        AssertDiffers(world, "player-walk-weapon.png", "player-attack-weapon.png");
        AssertDiffers(world, "npc-walk.png", "npc-attack.png");
        AssertDiffers(world, "npc-walk.png", "npc-death.png");
        AssertDiffers(world, "monster-walk.png", "monster-attack.png");
        AssertDiffers(world, "monster-walk.png", "monster-death.png");
        AssertSheetPng(Path.Combine(world, "player-attack-tunic.png"), 96, 128);
        AssertSheetPng(Path.Combine(world, "player-death-armor.png"), 96, 128);
        AssertSheetPng(Path.Combine(world, "player-death-hat.png"), 96, 128);
    }

    [Fact]
    public void DrawPath_ReusesPosePipeline_KeepsSparksAndHello()
    {
        var root = RepoRoot();
        var renderer = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "MapViewRenderer.cs"));
        var assets = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var world = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "WorldEntityAssets.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var csproj = File.ReadAllText(Path.Combine(root, "Frog.Client", "Frog.Client.csproj"));
        var generator = File.ReadAllText(Path.Combine(root, "tools", "generate-action-sprites.py"));
        var clock = File.ReadAllText(Path.Combine(root, "Frog.Core", "Gameplay", "ActionClock.cs"));

        Assert.Contains("PlayerWorldAssets.DrawFeetAnchored", renderer, StringComparison.Ordinal);
        Assert.Contains("WorldEntityAssets.DrawFeetAnchored", renderer, StringComparison.Ordinal);
        Assert.Contains("player-attack-body.png", assets, StringComparison.Ordinal);
        Assert.Contains("player-death-body.png", assets, StringComparison.Ordinal);
        Assert.Contains("ResolveSheets", assets, StringComparison.Ordinal);
        Assert.Contains("NativeSize = 32", assets, StringComparison.Ordinal);
        Assert.Contains("npc-attack.png", world, StringComparison.Ordinal);
        Assert.Contains("npc-death.png", world, StringComparison.Ordinal);
        Assert.Contains("monster-attack.png", world, StringComparison.Ordinal);
        Assert.Contains("monster-death.png", world, StringComparison.Ordinal);
        Assert.Contains("SpriteAction.Attack", world, StringComparison.Ordinal);
        Assert.Contains("SpriteAction.Death", world, StringComparison.Ordinal);

        Assert.Contains("BeginLocalAttack", shell, StringComparison.Ordinal);
        Assert.Contains("BeginLocalDeath", shell, StringComparison.Ordinal);
        Assert.Contains("ActionClock.IsIncomingAttack", shell, StringComparison.Ordinal);
        Assert.Contains("BeginNamedDeath", shell, StringComparison.Ordinal);
        Assert.Contains("_combatHud.Sparks", shell, StringComparison.Ordinal);
        Assert.Contains("localPose: localPose", shell, StringComparison.Ordinal);
        Assert.Contains("Mort signalée par le serveur.", shell, StringComparison.Ordinal);

        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-attack-body.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\player-death-head.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\npc-attack.png\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EmbeddedResource Include=\"Assets\\World\\monster-death.png\"", csproj, StringComparison.Ordinal);

        Assert.Contains("ATTACK_MAG", generator, StringComparison.Ordinal);
        Assert.Contains("FACING", generator, StringComparison.Ordinal);
        Assert.Contains("Never embeds Graal", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("amber", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urllib", generator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("urlopen", generator, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("IsIncomingAttack", clock, StringComparison.Ordinal);
        Assert.Contains("AttackDurationMs", clock, StringComparison.Ordinal);
        Assert.Equal(270, ActionClock.AttackDurationMs);
    }

    private static void AssertDiffers(string world, string leftName, string rightName)
    {
        var left = Path.Combine(world, leftName);
        var right = Path.Combine(world, rightName);
        AssertSheetPng(left, 96, 128);
        AssertSheetPng(right, 96, 128);
        Assert.NotEqual(File.ReadAllBytes(left), File.ReadAllBytes(right));
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
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

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
