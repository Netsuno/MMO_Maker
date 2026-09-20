using System;
using System.IO;
using Frog.Application.Demo;
using Frog.Core.Audio;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Core.Weather;
using Xunit;

namespace Frog.Tests;

public sealed class WeatherMvpTests
{
    [Fact]
    public void ProtocolVersion_Stays11_EnvironmentOpcodeUnchanged()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(74, (byte)PacketId.EnvironmentStatePush);
        Assert.Equal(Phase8Wire.EnvironmentStateCoreBytes, 38);
    }

    [Fact]
    public void Catalog_ClearRainFog_AndKnownPhase8Ids()
    {
        Assert.Equal(WeatherKindId.Clear, WeatherCatalog.Clear.KindId);
        Assert.Equal(WeatherKindId.Rain, WeatherCatalog.Rain.KindId);
        Assert.Equal(WeatherKindId.Fog, WeatherCatalog.Fog.KindId);
        Assert.False(WeatherCatalog.Clear.NeedsDraw);
        Assert.True(WeatherCatalog.Rain.NeedsDraw);
        Assert.True(WeatherCatalog.Fog.NeedsDraw);
        Assert.Equal(12, WeatherCatalog.Rain.ParticleCount);
        Assert.True(WeatherCatalog.Rain.ParticleCount <= WeatherParticles.MaxStreaks);
        Assert.Equal(0, WeatherCatalog.Fog.ParticleCount);
        Assert.Equal(Phase10DemoWorldCatalog.VillageWeatherId, WeatherCatalog.DemoVillageWeatherId);
        Assert.Equal(Phase10DemoWorldCatalog.WildsWeatherId, WeatherCatalog.DemoWildsWeatherId);
        Assert.True(WeatherCatalog.TryGetKindForProfile(WeatherCatalog.DemoWildsWeatherId, out var wilds));
        Assert.Equal(WeatherKindId.Rain, wilds);
        Assert.True(WeatherCatalog.TryGetKindForProfile(WeatherCatalog.Phase8RainWeatherId, out var p8rain));
        Assert.Equal(WeatherKindId.Rain, p8rain);
        Assert.False(WeatherCatalog.TryGetKindForProfile(Guid.NewGuid(), out _));
    }

    [Theory]
    [InlineData("rain", "rain")]
    [InlineData("PLUIE", "rain")]
    [InlineData("foggy", "fog")]
    [InlineData("brouillard", "fog")]
    [InlineData("clear", "clear")]
    [InlineData("snow", "clear")]
    [InlineData("", "clear")]
    public void NormalizeKind_AliasesAndUnknownFallback(string input, string expected)
    {
        Assert.Equal(expected, WeatherCatalog.NormalizeKind(input));
    }

    [Fact]
    public void Resolver_UsesPublishedKind_ThenProfileId_ThenClear()
    {
        var fromKind = WeatherResolver.Resolve(Guid.NewGuid(), 255, "fog");
        Assert.Equal(WeatherKindId.Fog, fromKind.KindId);

        var fromId = WeatherResolver.Resolve(WeatherCatalog.DemoWildsWeatherId, 140, publishedKind: null);
        Assert.Equal(WeatherKindId.Rain, fromId.KindId);
        Assert.True(fromId.TintAlpha > 0);

        var unknown = WeatherResolver.Resolve(Guid.NewGuid(), 255, publishedKind: null);
        Assert.Equal(WeatherKindId.Clear, unknown.KindId);
        Assert.False(unknown.NeedsDraw);
    }

    [Fact]
    public void Resolver_ProfileDefinition_AndDebugOverrideWins()
    {
        var profile = new WeatherProfileDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Tempête",
            WeatherKind = "rain",
            LightingFactor = 0.55f,
        };
        var published = WeatherResolver.Resolve(profile);
        Assert.Equal(WeatherKindId.Rain, published.KindId);
        Assert.True(published.WantsAmbience);

        var forcedFog = WeatherResolver.Resolve(profile, WeatherDebugOverride.Fog);
        Assert.Equal(WeatherKindId.Fog, forcedFog.KindId);

        Assert.Equal(WeatherDebugOverride.Clear, WeatherResolver.Cycle(WeatherDebugOverride.Auto));
        Assert.Equal(WeatherDebugOverride.Rain, WeatherResolver.Cycle(WeatherDebugOverride.Clear));
        Assert.Equal(WeatherDebugOverride.Fog, WeatherResolver.Cycle(WeatherDebugOverride.Rain));
        Assert.Equal(WeatherDebugOverride.Auto, WeatherResolver.Cycle(WeatherDebugOverride.Fog));
    }

    [Fact]
    public void Lighting_DarkensTint_WithoutExceedingCap()
    {
        var bright = WeatherResolver.ApplyLighting(WeatherCatalog.Clear, 255);
        Assert.False(bright.NeedsDraw);

        var night = WeatherResolver.ApplyLighting(WeatherCatalog.Clear, 0);
        Assert.True(night.NeedsDraw);
        Assert.InRange(night.TintAlpha, 1, 180);

        var rainNight = WeatherResolver.ApplyLighting(WeatherCatalog.Rain, 0);
        Assert.True(rainNight.TintAlpha >= WeatherCatalog.Rain.TintAlpha);
        Assert.True(rainNight.TintAlpha <= 180);
        Assert.Equal(12, rainNight.ParticleCount);
    }

    [Fact]
    public void Particles_AreBounded_Deterministic_AndSkipWhenClear()
    {
        Span<(int X, int Y, int Length)> a = stackalloc (int, int, int)[WeatherParticles.MaxStreaks];
        Span<(int X, int Y, int Length)> b = stackalloc (int, int, int)[WeatherParticles.MaxStreaks];
        var n1 = WeatherParticles.FillStreaks(WeatherCatalog.Rain, 320, 200, tickMs: 160, a);
        var n2 = WeatherParticles.FillStreaks(WeatherCatalog.Rain, 320, 200, tickMs: 160, b);
        Assert.Equal(12, n1);
        Assert.Equal(n1, n2);
        for (var i = 0; i < n1; i++)
        {
            Assert.Equal(a[i], b[i]);
            Assert.InRange(a[i].X, 0, 319);
            Assert.InRange(a[i].Y, 0, 199);
            Assert.InRange(a[i].Length, 6, 14);
        }

        Assert.Equal(0, WeatherParticles.FillStreaks(WeatherCatalog.Clear, 320, 200, 160, a));
        Assert.Equal(0, WeatherParticles.FillStreaks(WeatherCatalog.Fog, 320, 200, 160, a));
        Assert.Equal(0, WeatherParticles.FillStreaks(WeatherCatalog.Rain, 0, 200, 160, a));
    }

    [Fact]
    public void AudioGate_RespectsMuteAndVolume_NoNewEngine()
    {
        var mixer = new AudioMixer();
        mixer.SetVolume(80);
        Assert.True(WeatherAudio.ShouldPlayAmbience(WeatherCatalog.Rain, mixer));
        Assert.True(WeatherAudio.ShouldPlayAmbience(WeatherCatalog.Fog, mixer));
        Assert.False(WeatherAudio.ShouldPlayAmbience(WeatherCatalog.Clear, mixer));

        mixer.SetMuted(true);
        Assert.False(WeatherAudio.ShouldPlayAmbience(WeatherCatalog.Rain, mixer));

        mixer.SetMuted(false);
        mixer.SetVolume(0);
        Assert.False(WeatherAudio.ShouldPlayAmbience(WeatherCatalog.Rain, mixer));
        Assert.False(mixer.ShouldPlay(AudioCue.UiClick));
        Assert.False(mixer.ShouldPlay(AudioCue.MusicLoop));
    }

    [Fact]
    public void EnvironmentState_Legacy38Bytes_AndAdditiveKindTrailer()
    {
        var region = Guid.Parse("cccccccc-000c-4000-8000-000000000001");
        var weather = WeatherCatalog.DemoWildsWeatherId;
        var legacy = Phase8Wire.BuildEnvironmentState(2, region, weather, 140);
        Assert.Equal(Phase8Wire.EnvironmentStateCoreBytes, legacy.Length);
        Assert.True(Phase8Wire.TryParseEnvironmentState(
            legacy, out var map, out var rid, out var wid, out var light, out var kind));
        Assert.Equal(2, map);
        Assert.Equal(region, rid);
        Assert.Equal(weather, wid);
        Assert.Equal(140, light);
        Assert.Equal(string.Empty, kind);

        var withKind = Phase8Wire.BuildEnvironmentState(2, region, weather, 140, "rain");
        Assert.True(withKind.Length > Phase8Wire.EnvironmentStateCoreBytes);
        Assert.True(Phase8Wire.TryParseEnvironmentState(withKind, out _, out _, out _, out _, out var parsedKind));
        Assert.Equal("rain", parsedKind);

        // Old 4-out parser still accepts the additive trailer.
        Assert.True(Phase8Wire.TryParseEnvironmentState(withKind, out var map2, out _, out var wid2, out var light2));
        Assert.Equal(2, map2);
        Assert.Equal(weather, wid2);
        Assert.Equal(140, light2);
    }

    [Fact]
    public void ClientWiresOverlay_WithoutProtocolBumpOrExactShaPanelEdits()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var mapRenderer = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "MapViewRenderer.cs"));
        var overlay = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "WeatherOverlayRenderer.cs"));
        var sound = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Services", "SoundService.cs"));
        var help = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "HelpForm.cs"));
        var envPanel = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "EnvironmentPanel.cs"));
        var sender = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Network", "PacketSender.cs"));
        var handlers = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "Network", "Phase8GameplayHandlers.cs"));
        var client = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));

        Assert.Contains("WeatherResolver.Resolve", shell, StringComparison.Ordinal);
        Assert.Contains("weatherPlan: _weatherPlan", shell, StringComparison.Ordinal);
        Assert.Contains("weatherTickMs: _weatherTickMs", shell, StringComparison.Ordinal);
        Assert.Contains("CycleWeatherDebug", shell, StringComparison.Ordinal);
        Assert.Contains("Keys.F8", shell, StringComparison.Ordinal);
        Assert.Contains("_sound.ApplyWeather(_weatherPlan)", shell, StringComparison.Ordinal);
        Assert.Contains("WeatherOverlayRenderer.Draw", mapRenderer, StringComparison.Ordinal);
        Assert.Contains("WeatherOverlayRenderer.Draw(g, bmp.Size, weatherPlan, weatherTickMs)", mapRenderer, StringComparison.Ordinal);
        Assert.Contains("public static void Draw(", overlay, StringComparison.Ordinal);
        Assert.Contains("WeatherAudio.ShouldPlayAmbience", sound, StringComparison.Ordinal);
        Assert.Contains("F8 (en jeu)", help, StringComparison.Ordinal);
        Assert.Contains("snapshot.WeatherKind", handlers, StringComparison.Ordinal);
        Assert.Contains("weatherKind", sender, StringComparison.Ordinal);
        Assert.Contains("WeatherKind = envWeatherKind", client, StringComparison.Ordinal);

        Assert.Contains("Météo: {_weatherLookup(weather)}", envPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherResolver", envPanel, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
    }

    [Fact]
    public void StatusDoc_RecordsWeatherMvp()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "weather", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("pas de merge", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WeatherResolver", text, StringComparison.Ordinal);
        Assert.Contains("WeatherOverlayRenderer", text, StringComparison.Ordinal);
        Assert.Contains("WeatherAudio", text, StringComparison.Ordinal);
        Assert.Contains("F8", text, StringComparison.Ordinal);
        Assert.Contains("clear", text, StringComparison.Ordinal);
        Assert.Contains("rain", text, StringComparison.Ordinal);
        Assert.Contains("fog", text, StringComparison.Ordinal);
        Assert.Contains("FrogWireProtocol.Version", text, StringComparison.Ordinal);
        Assert.Contains("reste 11", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NAudio", text, StringComparison.Ordinal);
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
