using System.Media;
using Frog.Core.Audio;

namespace Frog.Client.Services;

/// <summary>
/// Lecture WAV via <see cref="SoundPlayer"/> (Win x64 self-contained, zéro NuGet).
/// Volume = gain PCM (SoundPlayer n’a pas de volume). Exceptions avalées.
/// </summary>
internal sealed class WindowsWavePlayback : IAudioPlayback, IDisposable
{
    private readonly Dictionary<AudioCue, byte[]> _source = [];
    private readonly Dictionary<AudioCue, PlayerSlot> _slots = [];
    private bool _disposed;

    public void Play(AudioCue cue, float gain)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!TryGetSource(cue, out var wav))
            {
                return;
            }

            var scaled = Math.Abs(gain - 1f) <= 0.0001f ? wav : WavPcm.ScaleAmplitude(wav, gain);
            var slot = GetSlot(cue);
            slot.Replace(scaled);
            if (cue == AudioCue.MusicLoop)
            {
                slot.Player.PlayLooping();
            }
            else
            {
                slot.Player.Play();
            }
        }
        catch
        {
            // pas de device / WAV manquant / session CI sans audio
        }
    }

    public void Stop(AudioCue cue)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_slots.TryGetValue(cue, out var slot))
            {
                slot.Player.Stop();
            }
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var slot in _slots.Values)
        {
            try
            {
                slot.Player.Stop();
            }
            catch
            {
                // ignore
            }

            slot.Dispose();
        }

        _slots.Clear();
    }

    private bool TryGetSource(AudioCue cue, out byte[] wav)
    {
        if (_source.TryGetValue(cue, out wav!))
        {
            return true;
        }

        var path = AudioAssetLocator.Find(cue);
        if (path is null)
        {
            wav = [];
            return false;
        }

        wav = File.ReadAllBytes(path);
        if (!WavPcm.TryRead(wav, out _, out _))
        {
            wav = [];
            return false;
        }

        _source[cue] = wav;
        return true;
    }

    private PlayerSlot GetSlot(AudioCue cue)
    {
        if (_slots.TryGetValue(cue, out var existing))
        {
            return existing;
        }

        var slot = new PlayerSlot();
        _slots[cue] = slot;
        return slot;
    }

    private sealed class PlayerSlot : IDisposable
    {
        public SoundPlayer Player { get; } = new();

        private MemoryStream? _stream;

        public void Replace(byte[] wav)
        {
            Player.Stop();
            _stream?.Dispose();
            _stream = new MemoryStream(wav, writable: false);
            Player.Stream = _stream;
            Player.Load();
        }

        public void Dispose()
        {
            Player.Dispose();
            _stream?.Dispose();
        }
    }
}
