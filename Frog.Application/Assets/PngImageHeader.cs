namespace Frog.Application.Assets;

/// <summary>Lit largeur/hauteur IHDR d’un PNG sans dépendance graphique.</summary>
public static class PngImageHeader
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryRead(ReadOnlySpan<byte> png, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (png.Length < 24)
        {
            return false;
        }

        if (!png[..8].SequenceEqual(Signature))
        {
            return false;
        }

        var length = (png[8] << 24) | (png[9] << 16) | (png[10] << 8) | png[11];
        if (length < 13)
        {
            return false;
        }

        if (png[12] != (byte)'I' || png[13] != (byte)'H' || png[14] != (byte)'D' || png[15] != (byte)'R')
        {
            return false;
        }

        width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return width > 0 && height > 0;
    }
}
