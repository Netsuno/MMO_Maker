using System.Buffers.Binary;
using System.IO.Compression;

namespace Frog.Server.Content;

/// <summary>
/// PNG 8 bits non entrelacé, RGB ou RGBA. Pas de palette, pas de tRNS : l’alpha droit vient du type couleur 6,
/// ou est opaque pour le type 2. Le reliquat de découpe 48×48 est laissé à <c>TileSheetSlicer</c>.
/// </summary>
public static class PngRgba8
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> straightRgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var expected = checked(width * height * 4);
        if (straightRgba.Length != expected)
        {
            throw new ArgumentException("RGBA droit : largeur × hauteur × 4.", nameof(straightRgba));
        }

        var scanlines = new byte[height * (1 + (width * 4))];
        var stride = width * 4;
        for (var y = 0; y < height; y++)
        {
            var dest = y * (1 + stride);
            scanlines[dest] = 0;
            straightRgba.Slice(y * stride, stride).CopyTo(scanlines.AsSpan(dest + 1, stride));
        }

        var idat = ZlibCompress(scanlines);
        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(output, "IHDR"u8, ihdr);
        WriteChunk(output, "IDAT"u8, idat);
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    public static (int Width, int Height, byte[] StraightRgba) Decode(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Signature))
        {
            throw new InvalidDataException("Signature PNG absente.");
        }

        var haveHeader = false;
        var width = 0;
        var height = 0;
        byte colorType = 0;
        using var idat = new MemoryStream();
        var offset = 8;
        var ended = false;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.Slice(offset, 4));
            if (length < 0 || offset + 12L + length > png.Length)
            {
                throw new InvalidDataException("Chunk PNG tronqué.");
            }

            var type = png.Slice(offset + 4, 4);
            var data = png.Slice(offset + 8, length);
            var declared = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(offset + 8 + length, 4));
            if (declared != Crc(type, data))
            {
                throw new InvalidDataException("CRC PNG invalide.");
            }

            offset += 12 + length;
            if (type.SequenceEqual("IHDR"u8))
            {
                if (data.Length < 13)
                {
                    throw new InvalidDataException("IHDR trop court.");
                }

                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                var bitDepth = data[8];
                colorType = data[9];
                if (width <= 0 || height <= 0)
                {
                    throw new InvalidDataException("Dimensions PNG invalides.");
                }

                if (bitDepth != 8 || data[10] != 0 || data[11] != 0 || data[12] != 0)
                {
                    throw new InvalidDataException("PNG V1 : 8 bits, sans entrelacement. Pas d’upscale silencieux.");
                }

                if (colorType is not (2 or 6))
                {
                    throw new InvalidDataException("PNG V1 : RGB ou RGBA uniquement (pas de palette).");
                }

                haveHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                idat.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                ended = true;
                break;
            }
            else if (type.SequenceEqual("tRNS"u8))
            {
                throw new InvalidDataException("PNG tRNS refusé. Exportez en RGBA8.");
            }
            else if ((type[0] & 0x20) == 0)
            {
                throw new InvalidDataException("Chunk PNG critique inconnu.");
            }
        }

        if (!haveHeader || !ended || idat.Length == 0)
        {
            throw new InvalidDataException("PNG incomplet.");
        }

        var channels = colorType == 6 ? 4 : 3;
        var raw = ZlibDecompress(idat.GetBuffer().AsSpan(0, (int)idat.Length));
        var expected = height * (1 + (width * channels));
        if (raw.Length != expected)
        {
            throw new InvalidDataException("Taille des scanlines PNG incohérente.");
        }

        var rgba = Unfilter(raw, width, height, channels);
        return (width, height, rgba);
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int channels)
    {
        var stride = width * channels;
        var recon = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            var src = y * (stride + 1) + 1;
            var dst = y * stride;
            for (var x = 0; x < stride; x++)
            {
                var left = x >= channels ? recon[dst + x - channels] : (byte)0;
                var up = y > 0 ? recon[dst - stride + x] : (byte)0;
                var upLeft = y > 0 && x >= channels ? recon[dst - stride + x - channels] : (byte)0;
                var predictor = filter switch
                {
                    0 => (byte)0,
                    1 => left,
                    2 => up,
                    3 => (byte)((left + up) / 2),
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("Filtre PNG inconnu."),
                };
                recon[dst + x] = (byte)(raw[src + x] + predictor);
            }
        }

        if (channels == 4)
        {
            return recon;
        }

        var expanded = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            expanded[i * 4] = recon[i * 3];
            expanded[(i * 4) + 1] = recon[(i * 3) + 1];
            expanded[(i * 4) + 2] = recon[(i * 3) + 2];
            expanded[(i * 4) + 3] = 255;
        }

        return expanded;
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

        if (pb <= pc)
        {
            return b;
        }

        return c;
    }

    private static byte[] ZlibCompress(ReadOnlySpan<byte> data)
    {
        using var raw = new MemoryStream();
        using (var deflate = new DeflateStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(data);
        }

        var deflated = raw.ToArray();
        var output = new byte[2 + deflated.Length + 4];
        output[0] = 0x78;
        output[1] = 0x01;
        deflated.CopyTo(output.AsSpan(2));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(output.Length - 4), Adler32(data));
        return output;
    }

    private static byte[] ZlibDecompress(ReadOnlySpan<byte> zlib)
    {
        if (zlib.Length < 6 || (zlib[0] & 0x0F) != 8)
        {
            throw new InvalidDataException("Flux zlib PNG invalide.");
        }

        var payload = zlib.Slice(2, zlib.Length - 6).ToArray();
        using var input = new MemoryStream(payload);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(type, data));
        output.Write(crc);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var value in type)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
