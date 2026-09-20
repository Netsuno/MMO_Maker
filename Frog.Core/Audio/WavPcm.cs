using System.Buffers.Binary;

namespace Frog.Core.Audio;

/// <summary>PCM 16-bit WAV minimal (lecture / écriture / gain). Pas de compression.</summary>
public static class WavPcm
{
    public const int BitsPerSample = 16;

    public static byte[] WriteMono16(int sampleRate, ReadOnlySpan<short> samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        var dataSize = samples.Length * sizeof(short);
        var bytes = new byte[44 + dataSize];
        WriteAscii(bytes, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 36 + dataSize);
        WriteAscii(bytes, 8, "WAVE");
        WriteAscii(bytes, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), sampleRate * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), BitsPerSample);
        WriteAscii(bytes, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), dataSize);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + (i * 2)), samples[i]);
        }

        return bytes;
    }

    public static bool TryRead(ReadOnlySpan<byte> wav, out WavPcmInfo info, out short[] samples)
    {
        info = default;
        samples = [];
        if (wav.Length < 44
            || !AsciiEquals(wav, 0, "RIFF")
            || !AsciiEquals(wav, 8, "WAVE"))
        {
            return false;
        }

        var offset = 12;
        var sampleRate = 0;
        var channels = 0;
        var bits = 0;
        var pcm = false;
        short[]? data = null;
        while (offset + 8 <= wav.Length)
        {
            var chunkId = wav.Slice(offset, 4);
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(offset + 4, 4));
            if (chunkSize < 0 || offset + 8 + chunkSize > wav.Length)
            {
                return false;
            }

            var payload = wav.Slice(offset + 8, chunkSize);
            if (AsciiEquals(chunkId, 0, "fmt "))
            {
                if (chunkSize < 16)
                {
                    return false;
                }

                pcm = BinaryPrimitives.ReadInt16LittleEndian(payload) == 1;
                channels = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(14));
            }
            else if (AsciiEquals(chunkId, 0, "data"))
            {
                if (bits != BitsPerSample || payload.Length % 2 != 0)
                {
                    return false;
                }

                data = new short[payload.Length / 2];
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(i * 2));
                }
            }

            offset += 8 + chunkSize;
            if ((chunkSize & 1) != 0)
            {
                offset++;
            }
        }

        if (!pcm || sampleRate <= 0 || channels is < 1 or > 2 || bits != BitsPerSample || data is null)
        {
            return false;
        }

        info = new WavPcmInfo
        {
            SampleRate = sampleRate,
            Channels = channels,
            BitsPerSample = bits,
            SampleCount = data.Length,
        };
        samples = data;
        return true;
    }

    public static byte[] ScaleAmplitude(ReadOnlySpan<byte> wav, float gain)
    {
        if (!TryRead(wav, out var info, out var samples))
        {
            throw new InvalidOperationException("WAV PCM 16-bit invalide.");
        }

        var clamped = Math.Clamp(gain, 0f, 4f);
        if (clamped == 0f)
        {
            Array.Clear(samples);
        }
        else if (Math.Abs(clamped - 1f) > 0.0001f)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                var scaled = (int)Math.Round(samples[i] * (double)clamped);
                samples[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            }
        }

        return info.Channels == 1
            ? WriteMono16(info.SampleRate, samples)
            : WriteStereo16(info.SampleRate, samples);
    }

    private static byte[] WriteStereo16(int sampleRate, ReadOnlySpan<short> interleaved)
    {
        var dataSize = interleaved.Length * sizeof(short);
        var bytes = new byte[44 + dataSize];
        WriteAscii(bytes, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 36 + dataSize);
        WriteAscii(bytes, 8, "WAVE");
        WriteAscii(bytes, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), sampleRate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), BitsPerSample);
        WriteAscii(bytes, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), dataSize);
        for (var i = 0; i < interleaved.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + (i * 2)), interleaved[i]);
        }

        return bytes;
    }

    private static void WriteAscii(byte[] dest, int offset, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            dest[offset + i] = (byte)text[i];
        }
    }

    private static bool AsciiEquals(ReadOnlySpan<byte> span, int offset, string text)
    {
        if (offset + text.Length > span.Length)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (span[offset + i] != (byte)text[i])
            {
                return false;
            }
        }

        return true;
    }
}

public readonly struct WavPcmInfo
{
    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public int BitsPerSample { get; init; }

    public int SampleCount { get; init; }
}
