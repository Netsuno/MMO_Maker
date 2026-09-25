using System;
using System.IO;
using Frog.Client.Config;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Rebind + échelle UI : persistance JSON locale, clamp par pas de 25, tuile monde 32 inchangée.
/// </summary>
public sealed class ClientOptionsBindingScaleTests
{
    [Fact]
    public void SettingsStore_RoundTripsBindings_Fullscreen_AndUiScale()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-options-bind-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "client-settings.json");
        try
        {
            var store = new ClientSettingsStore(path);
            var fresh = store.Load();
            Assert.Equal(KeyboardLayoutPreset.Azerty, fresh.KeyboardPreset);
            Assert.Equal("Z", fresh.Bindings.MoveUp);
            Assert.Equal("Q", fresh.Bindings.MoveLeft);
            Assert.Equal("S", fresh.Bindings.MoveDown);
            Assert.Equal("D", fresh.Bindings.MoveRight);
            Assert.Equal("E", fresh.Bindings.Interact);
            Assert.Equal("Space", fresh.Bindings.Attack);
            Assert.False(fresh.Window.FullScreen);
            Assert.Equal(ClientUiScale.DefaultPercent, fresh.UiScalePercent);
            Assert.Equal(360, ClientUiScale.ScaleDip(360, fresh.UiScalePercent));
            Assert.Equal(400, ClientUiScale.ScaleDip(400, fresh.UiScalePercent));
            Assert.Equal(520, ClientUiScale.ScaleDip(520, fresh.UiScalePercent));

            fresh.Bindings.MoveUp = "I";
            fresh.Bindings.MoveDown = "K";
            fresh.Bindings.MoveLeft = "J";
            fresh.Bindings.MoveRight = "L";
            fresh.Bindings.Interact = "F";
            fresh.Bindings.Attack = "R";
            fresh.Window.FullScreen = true;
            fresh.UiScalePercent = 150;
            store.Save(fresh);

            Assert.False(File.Exists(path + ".tmp"));
            var json = File.ReadAllText(path);
            Assert.Contains("\"moveUp\": \"I\"", json, StringComparison.Ordinal);
            Assert.Contains("\"moveLeft\": \"J\"", json, StringComparison.Ordinal);
            Assert.Contains("\"interact\": \"F\"", json, StringComparison.Ordinal);
            Assert.Contains("\"attack\": \"R\"", json, StringComparison.Ordinal);
            Assert.Contains("\"fullScreen\": true", json, StringComparison.Ordinal);
            Assert.Contains("\"uiScalePercent\": 150", json, StringComparison.Ordinal);

            var reloaded = new ClientSettingsStore(path).Load();
            Assert.Equal("I", reloaded.Bindings.MoveUp);
            Assert.Equal("K", reloaded.Bindings.MoveDown);
            Assert.Equal("J", reloaded.Bindings.MoveLeft);
            Assert.Equal("L", reloaded.Bindings.MoveRight);
            Assert.Equal("F", reloaded.Bindings.Interact);
            Assert.Equal("R", reloaded.Bindings.Attack);
            Assert.True(reloaded.Window.FullScreen);
            Assert.Equal(150, reloaded.UiScalePercent);
            Assert.Equal(32, ClientUiScale.WorldTilePixels);
            Assert.Equal(48, ClientUiScale.ScaleDip(ClientUiScale.WorldTilePixels, reloaded.UiScalePercent));
            Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void SettingsStore_LegacyJson_DefaultsScaleAndAttack_ClampsOutOfRange()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-options-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "client-settings.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "schemaVersion": 1,
                  "keyboardPreset": "qwerty",
                  "bindings": { "moveUp": "W", "moveLeft": "A" },
                  "window": { "fullScreen": true, "width": 10, "height": 10 }
                }
                """);

            var loaded = new ClientSettingsStore(path).Load();
            Assert.Equal(KeyboardLayoutPreset.Qwerty, loaded.KeyboardPreset);
            Assert.Equal("W", loaded.Bindings.MoveUp);
            Assert.Equal("A", loaded.Bindings.MoveLeft);
            Assert.Equal("S", loaded.Bindings.MoveDown);
            Assert.Equal("D", loaded.Bindings.MoveRight);
            Assert.Equal("E", loaded.Bindings.Interact);
            Assert.Equal("Space", loaded.Bindings.Attack);
            Assert.True(loaded.Window.FullScreen);
            Assert.Equal(980, loaded.Window.Width);
            Assert.Equal(640, loaded.Window.Height);
            Assert.Equal(100, loaded.UiScalePercent);

            loaded.UiScalePercent = 999;
            loaded.Bindings.Attack = "  ";
            new ClientSettingsStore(path).Save(loaded);
            var clamped = new ClientSettingsStore(path).Load();
            Assert.Equal(ClientUiScale.MaxPercent, clamped.UiScalePercent);
            Assert.Equal("Space", clamped.Bindings.Attack);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-20, 100)]
    [InlineData(50, 75)]
    [InlineData(75, 75)]
    [InlineData(87, 75)]
    [InlineData(88, 100)]
    [InlineData(100, 100)]
    [InlineData(112, 100)]
    [InlineData(113, 125)]
    [InlineData(150, 150)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(250, 200)]
    [InlineData(999, 200)]
    public void UiScale_Clamp_SnapsToStepAndBounds(int raw, int expected)
    {
        Assert.Equal(expected, ClientUiScale.ClampPercent(raw));
        var settings = new UserSettings { UiScalePercent = raw };
        settings.Normalize();
        Assert.Equal(expected, settings.UiScalePercent);
        var clone = settings.Clone();
        Assert.Equal(expected, clone.UiScalePercent);
    }

    [Fact]
    public void UiScale_HundredPercent_IsIdentity_AndDoesNotResizeWorldTile()
    {
        Assert.Equal(32, ClientUiScale.ScaleDip(32, 100));
        Assert.Equal(360, ClientUiScale.ScaleDip(360, 100));
        Assert.Equal(8, ClientUiScale.ScaleDip(8, 100));
        Assert.Equal(10f, ClientUiScale.ScaleEm(10f, 100));
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, ClientUiScale.ScaleDip(32, 150));
        Assert.NotEqual(WorldMetrics.DefaultTileSizePixels, ClientUiScale.ScaleDip(32, 150));
        Assert.Equal(1, ClientUiScale.StepIndex(100));
        Assert.Equal(3, ClientUiScale.StepIndex(150));
    }

    [Fact]
    public void Client_OptionsScale_LeavesHelloAndTilePathUntouched()
    {
        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var metrics = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "WorldMetrics.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var renderer = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "MapViewRenderer.cs"));
        var options = File.ReadAllText(Path.Combine(root, "Frog.Client", "Forms", "OptionsForm.cs"));
        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var applicator = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "UiScaleApplicator.cs"));
        var input = File.ReadAllText(Path.Combine(root, "Frog.Client", "Services", "InputService.cs"));

        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.Contains("public const int DefaultTileSizePixels = 32;", metrics, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("WorldMetrics.DefaultTileSizePixels", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("UiScalePercent", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientUiScale", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceDpi", applicator, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceDpi", shell, StringComparison.Ordinal);
        Assert.Contains("PictureBox", applicator, StringComparison.Ordinal);
        Assert.Contains("Échelle de l'interface", options, StringComparison.Ordinal);
        Assert.Contains("Plein écran", options, StringComparison.Ordinal);
        Assert.Contains("\"Attack\"", options, StringComparison.Ordinal);
        Assert.Contains("IsAttack", shell, StringComparison.Ordinal);
        Assert.Contains("ApplyUiScale", shell, StringComparison.Ordinal);
        Assert.Contains("Keys.Space", input, StringComparison.Ordinal);
        Assert.Contains("IsAttack", input, StringComparison.Ordinal);
    }

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
