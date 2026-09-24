using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace Frog.Editor.Services;

/// <summary>
/// PNG RGBA8 non entrelacé, assez pour les vignettes 48×48 et les feuilles déjà en RGBA/RGB 8 bits.
/// Les PNG indexés passent par System.Drawing dans l’UI.
/// </summary>
public static class TileAssetPngCodec
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(ReadOnlySpan<byte> straightRgba, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var expected = checked(width * height * 4);
        if (straightRgba.Length != expected)
        {
            throw new ArgumentException("Le buffer RGBA ne correspond pas à largeur × hauteur × 4.", nameof(straightRgba));
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(output, "IHDR"u8, ihdr);

        var rawLength = checked((width * 4 + 1) * height);
        var raw = new byte[rawLength];
        var rowBytes = width * 4;
        for (var y = 0; y < height; y++)
        {
            var dst = y * (rowBytes + 1);
            raw[dst] = 0;
            straightRgba.Slice(y * rowBytes, rowBytes).CopyTo(raw.AsSpan(dst + 1, rowBytes));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WriteChunk(output, "IDAT"u8, compressed.ToArray());
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> png, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = Array.Empty<byte>();
        if (png.Length < Signature.Length + 8 || !png[..Signature.Length].SequenceEqual(Signature))
        {
            return false;
        }

        var offset = Signature.Length;
        byte bitDepth = 0;
        byte colorType = 0;
        var idat = new MemoryStream();
        var sawHeader = false;
        var sawEnd = false;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(png[offset..]);
            if (length > int.MaxValue - 12 || offset + 12L + length > png.Length)
            {
                return false;
            }

            var type = png.Slice(offset + 4, 4);
            var data = png.Slice(offset + 8, (int)length);
            var crcSpan = png.Slice(offset + 4, 4 + (int)length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(png[(offset + 8 + (int)length)..]);
            if (Crc(crcSpan) != expectedCrc)
            {
                return false;
            }

            if (type.SequenceEqual("IHDR"u8))
            {
                if (data.Length != 13)
                {
                    return false;
                }

                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]));
                bitDepth = data[8];
                colorType = data[9];
                if (data[10] != 0 || data[11] != 0 || data[12] != 0)
                {
                    return false;
                }

                sawHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                break;
            }

            offset += 12 + (int)length;
        }

        if (!sawHeader || !sawEnd || width <= 0 || height <= 0 || bitDepth != 8 || colorType is not (2 or 6))
        {
            return false;
        }

        var channels = colorType == 6 ? 4 : 3;
        byte[] inflated;
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(idat.ToArray()), CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);
            inflated = raw.ToArray();
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var rowBytes = width * channels;
        var expectedRaw = (rowBytes + 1) * height;
        if (inflated.Length < expectedRaw)
        {
            return false;
        }

        var recon = new byte[rowBytes * height];
        var previous = new byte[rowBytes];
        var current = new byte[rowBytes];
        for (var y = 0; y < height; y++)
        {
            var filter = inflated[y * (rowBytes + 1)];
            var src = inflated.AsSpan((y * (rowBytes + 1)) + 1, rowBytes);
            if (!Unfilter(filter, src, previous, current, channels))
            {
                return false;
            }

            current.CopyTo(recon.AsSpan(y * rowBytes, rowBytes));
            (previous, current) = (current, previous);
        }

        if (channels == 4)
        {
            rgba = recon;
            return true;
        }

        rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            rgba[i * 4] = recon[i * 3];
            rgba[(i * 4) + 1] = recon[(i * 3) + 1];
            rgba[(i * 4) + 2] = recon[(i * 3) + 2];
            rgba[(i * 4) + 3] = 255;
        }

        return true;
    }

    private static bool Unfilter(byte filter, ReadOnlySpan<byte> src, ReadOnlySpan<byte> previous, Span<byte> dest, int bpp)
    {
        if (src.Length != dest.Length)
        {
            return false;
        }

        for (var i = 0; i < src.Length; i++)
        {
            var left = i >= bpp ? dest[i - bpp] : (byte)0;
            var up = previous[i];
            var upLeft = i >= bpp ? previous[i - bpp] : (byte)0;
            dest[i] = filter switch
            {
                0 => src[i],
                1 => (byte)(src[i] + left),
                2 => (byte)(src[i] + up),
                3 => (byte)(src[i] + ((left + up) / 2)),
                4 => (byte)(src[i] + Paeth(left, up, upLeft)),
                _ => (byte)0,
            };
            if (filter > 4)
            {
                return false;
            }
        }

        return true;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        var crcInput = new byte[type.Length + data.Length];
        type.CopyTo(crcInput);
        data.CopyTo(crcInput.AsSpan(type.Length));
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(crcInput));
        output.Write(crc);
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            c = CrcTable[(c ^ value) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
