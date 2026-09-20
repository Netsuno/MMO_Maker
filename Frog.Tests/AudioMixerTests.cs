using System;
using System.IO;
using Frog.Core.Audio;
using Xunit;

namespace Frog.Tests;

public sealed class AudioMixerTests
{
    [Fact]
    public void Defaults_Volume80_NotMuted_MusicOff()
    {
        var mixer = new AudioMixer();
        Assert.Equal(AudioMixer.DefaultVolumePercent, mixer.VolumePercent);
        Assert.False(mixer.MuteRequested);
        Assert.False(mixer.IsMuted);
        Assert.False(mixer.MusicEnabled);
        Assert.Equal(0.8f, mixer.Gain, precision: 3);
        Assert.True(mixer.ShouldPlay(AudioCue.UiClick));
        Assert.False(mixer.ShouldPlay(AudioCue.MusicLoop));
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(250, 100)]
    public void SetVolume_ClampsTo0_100(int input, int expected)
    {
        var mixer = new AudioMixer();
        mixer.SetVolume(input);
        Assert.Equal(expected, mixer.VolumePercent);
        Assert.Equal(expected / 100f, mixer.Gain, precision: 3);
        Assert.Equal(expected <= 0, mixer.IsMuted);
    }

    [Fact]
    public void ExplicitMute_ZeroesGain_EvenWhenVolumePositive()
    {
        var mixer = new AudioMixer();
        mixer.SetVolume(80);
        mixer.SetMuted(true);
        Assert.True(mixer.MuteRequested);
        Assert.True(mixer.IsMuted);
        Assert.Equal(0f, mixer.Gain);
        Assert.False(mixer.ShouldPlay(AudioCue.UiClick));
        mixer.SetMuted(false);
        Assert.False(mixer.IsMuted);
        Assert.Equal(0.8f, mixer.Gain, precision: 3);
    }

    [Fact]
    public void VolumeZero_IsMuted_WithoutExplicitFlag()
    {
        var mixer = new AudioMixer();
        mixer.SetVolume(0);
        Assert.False(mixer.MuteRequested);
        Assert.True(mixer.IsMuted);
        Assert.Equal(0f, mixer.SfxGain);
    }

    [Fact]
    public void MusicToggle_GatesMusicGain_Only()
    {
        var mixer = new AudioMixer();
        mixer.SetVolume(50);
        Assert.Equal(0.5f, mixer.SfxGain, precision: 3);
        Assert.Equal(0f, mixer.MusicGain);
        mixer.SetMusicEnabled(true);
        Assert.Equal(0.5f, mixer.MusicGain, precision: 3);
        mixer.SetMuted(true);
        Assert.Equal(0f, mixer.MusicGain);
    }

    [Fact]
    public void Play_RecordsSfx_WhenAudible_AndSkipsWhenMuted()
    {
        var rec = new RecordingAudioPlayback();
        var mixer = new AudioMixer(rec);
        Assert.True(mixer.Play(AudioCue.UiClick));
        Assert.Single(rec.Plays);
        Assert.Equal(AudioCue.UiClick, rec.Plays[0].Cue);
        Assert.Equal(0.8f, rec.Plays[0].Gain, precision: 3);

        mixer.SetMuted(true);
        Assert.False(mixer.Play(AudioCue.UiClick));
        Assert.Single(rec.Plays);
    }

    [Fact]
    public void Play_Music_StartsWhenEnabled_StopsWhenMutedOrDisabled()
    {
        var rec = new RecordingAudioPlayback();
        var mixer = new AudioMixer(rec);
        Assert.False(mixer.Play(AudioCue.MusicLoop));
        Assert.Contains(AudioCue.MusicLoop, rec.Stops);
        Assert.Empty(rec.Plays);

        mixer.SetMusicEnabled(true);
        Assert.True(mixer.Play(AudioCue.MusicLoop));
        Assert.Equal(AudioCue.MusicLoop, rec.Plays[0].Cue);

        mixer.SetMuted(true);
        Assert.False(mixer.Play(AudioCue.MusicLoop));
        Assert.Equal(2, rec.Stops.Count);

        mixer.SetMuted(false);
        mixer.SetMusicEnabled(false);
        Assert.False(mixer.Play(AudioCue.MusicLoop));
        Assert.Equal(3, rec.Stops.Count);
    }

    [Fact]
    public void Apply_WritesNormalizedMuteVolumeAndMusic()
    {
        var mixer = new AudioMixer();
        mixer.Apply(volumePercent: 140, muted: true, musicEnabled: true);
        Assert.Equal(100, mixer.VolumePercent);
        Assert.True(mixer.MuteRequested);
        Assert.True(mixer.MusicEnabled);
        Assert.True(mixer.IsMuted);
        Assert.Equal(0f, mixer.Gain);
    }
}

public sealed class WavPcmTests
{
    [Fact]
    public void WriteRead_RoundTrip_Mono16()
    {
        short[] source = [0, 1234, -3000, 32767, -32768];
        var wav = WavPcm.WriteMono16(22050, source);
        Assert.True(WavPcm.TryRead(wav, out var info, out var samples));
        Assert.Equal(22050, info.SampleRate);
        Assert.Equal(1, info.Channels);
        Assert.Equal(16, info.BitsPerSample);
        Assert.Equal(source, samples);
    }

    [Fact]
    public void ScaleAmplitude_ZeroGain_SilencesSamples()
    {
        var wav = WavPcm.WriteMono16(8000, [1000, -1000, 2000]);
        var scaled = WavPcm.ScaleAmplitude(wav, 0f);
        Assert.True(WavPcm.TryRead(scaled, out _, out var samples));
        Assert.All(samples, s => Assert.Equal(0, s));
    }

    [Fact]
    public void ScaleAmplitude_Half_ApproximatelyHalves()
    {
        var wav = WavPcm.WriteMono16(8000, [2000, -4000]);
        var scaled = WavPcm.ScaleAmplitude(wav, 0.5f);
        Assert.True(WavPcm.TryRead(scaled, out _, out var samples));
        Assert.Equal(1000, samples[0]);
        Assert.Equal(-2000, samples[1]);
    }

    [Fact]
    public void TryRead_RejectsGarbage()
    {
        Assert.False(WavPcm.TryRead("not a wav"u8.ToArray(), out _, out _));
        Assert.False(WavPcm.TryRead([], out _, out _));
    }
}

public sealed class AudioPlaceholderAssetTests
{
    [Fact]
    public void PlaceholderWavs_AreOriginalPcm16_InRepo()
    {
        var click = File.ReadAllBytes(Path.Combine(AudioDir(), AudioAssetNames.UiClickFile));
        var music = File.ReadAllBytes(Path.Combine(AudioDir(), AudioAssetNames.MusicLoopFile));
        Assert.True(WavPcm.TryRead(click, out var clickInfo, out var clickSamples));
        Assert.True(WavPcm.TryRead(music, out var musicInfo, out var musicSamples));
        Assert.Equal(22050, clickInfo.SampleRate);
        Assert.Equal(22050, musicInfo.SampleRate);
        Assert.Equal(1, clickInfo.Channels);
        Assert.Equal(1, musicInfo.Channels);
        Assert.True(clickSamples.Length > 500);
        Assert.True(clickSamples.Length < 8000);
        Assert.Equal(22050 * 2, musicSamples.Length);
        Assert.Contains(clickSamples, s => s != 0);
        Assert.Contains(musicSamples, s => s != 0);
        Assert.Equal(0, musicSamples[0]);
        var wrap = Math.Abs(musicSamples[0] - musicSamples[^1]);
        var step = Math.Abs(musicSamples[1] - musicSamples[0]);
        Assert.InRange(wrap, 0, step + 8);
    }

    [Fact]
    public void StatusDoc_OwnerNetsun_RecordsMuteVolumeApi()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "audio", "STATUS.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("AudioMixer", text, StringComparison.Ordinal);
        Assert.Contains("PlayUiClick", text, StringComparison.Ordinal);
        Assert.Contains("MusicEnabled", text, StringComparison.Ordinal);
        Assert.Contains("ui-click.wav", text, StringComparison.Ordinal);
        Assert.Contains("music-loop.wav", text, StringComparison.Ordinal);
        Assert.Contains("CC0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NAudio", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientHooks_UiClickOnOpenOptions_NoNAudioPackage()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Frog.Client.csproj"));
        var options = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "OptionsForm.cs"));
        Assert.Contains("_sound.PlayUiClick()", shell, StringComparison.Ordinal);
        Assert.Contains("Assets\\Audio\\**\\*", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("NAudio", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AudioMuted", options, StringComparison.Ordinal);
        Assert.Contains("MusicEnabled", options, StringComparison.Ordinal);
    }

    private static string AudioDir() => Path.Combine(RepoRoot(), "Frog.Client", "Assets", "Audio");

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
