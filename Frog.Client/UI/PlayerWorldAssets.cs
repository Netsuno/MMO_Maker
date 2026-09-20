#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace Frog.Client.UI;

/// <summary>
/// Original CC0 16×16 top-down player (authored for FRoG, not a third-party pack).
/// File: <c>Assets/World/player.png</c>. Fallback raster matches that drawing.
/// </summary>
internal static class PlayerWorldAssets
{
    public const string RelativePath = "Assets/World/player.png";
    public const int NativeSize = 16;
    public const int DrawScale = 2;

    private const string EmbeddedName = "Frog.Client.Assets.World.player.png";

    private static readonly object Gate = new();
    private static Bitmap? _sprite;
    private static bool _resolved;

    internal static int DrawnSizePixels => NativeSize * DrawScale;

    internal static Bitmap Sprite
    {
        get
        {
            EnsureLoaded();
            return _sprite!;
        }
    }

    internal static string? ResolvedPathForTest
    {
        get
        {
            EnsureLoaded();
            return _resolvedPath;
        }
    }

    private static string? _resolvedPath;

    internal static ImageAttributes CreateOtherPlayerTintAttributes()
    {
        // Cool shift so others read apart from local. Not a gold ColorMatrix.
        var matrix = new ColorMatrix(new float[][]
        {
            new float[] { 0.70f, 0f, 0f, 0f, 0f },
            new float[] { 0f, 0.80f, 0f, 0f, 0f },
            new float[] { 0f, 0f, 1.20f, 0f, 0f },
            new float[] { 0f, 0f, 0f, 1f, 0f },
            new float[] { 0.00f, 0.04f, 0.16f, 0f, 1f },
        });
        var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        attrs.SetWrapMode(WrapMode.TileFlipXY);
        return attrs;
    }

    internal static void DrawCentered(Graphics g, float centerXPx, float centerYPx, bool other)
    {
        ArgumentNullException.ThrowIfNull(g);
        var sprite = Sprite;
        var dw = sprite.Width * DrawScale;
        var dh = sprite.Height * DrawScale;
        var dest = new Rectangle(
            (int)MathF.Round(centerXPx - dw / 2f),
            (int)MathF.Round(centerYPx - dh / 2f),
            dw,
            dh);
        var src = new Rectangle(0, 0, sprite.Width, sprite.Height);

        var prevInterp = g.InterpolationMode;
        var prevSmooth = g.SmoothingMode;
        var prevOffset = g.PixelOffsetMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        try
        {
            if (other)
            {
                using var attrs = CreateOtherPlayerTintAttributes();
                g.DrawImage(sprite, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            else
            {
                g.DrawImage(sprite, dest, src, GraphicsUnit.Pixel);
            }
        }
        finally
        {
            g.InterpolationMode = prevInterp;
            g.SmoothingMode = prevSmooth;
            g.PixelOffsetMode = prevOffset;
        }
    }

    private static void EnsureLoaded()
    {
        if (_resolved)
        {
            return;
        }

        lock (Gate)
        {
            if (_resolved)
            {
                return;
            }

            _sprite = TryLoadFromFile() ?? TryLoadEmbedded() ?? CreateFallbackRaster();
            _resolved = true;
        }
    }

    private static Bitmap? TryLoadFromFile()
    {
        foreach (var path in FileCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var tmp = new Bitmap(path);
                if (tmp.Width != NativeSize || tmp.Height != NativeSize)
                {
                    continue;
                }

                _resolvedPath = path;
                return new Bitmap(tmp);
            }
            catch
            {
                // try next candidate
            }
        }

        return null;
    }

    private static Bitmap? TryLoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(EmbeddedName);
        if (stream is null)
        {
            return null;
        }

        try
        {
            using var tmp = new Bitmap(stream);
            return new Bitmap(tmp);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> FileCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "World", "player.png");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "Assets", "World", "player.png");
            yield return Path.Combine(dir.FullName, "Frog.Client", "Assets", "World", "player.png");
            dir = dir.Parent;
        }
    }

    /// <summary>Same original drawing as <c>tools/generate-player-sprite.py</c> if the PNG is missing.</summary>
    private static Bitmap CreateFallbackRaster()
    {
        const string map =
            "................" +
            "....KKKKKKKK...." +
            "...KHHHHHHHHK..." +
            "..KHHLLLLLLHHK.." +
            "..KHLLSSSSLLHK.." +
            "..KHLSESSSELHK.." +
            "...KHSSDDSSHK..." +
            "..KNNCCPPCCNNK.." +
            ".KNCCCTTTTCCCNK." +
            "KNCCCTTTTTTCCCNK" +
            "KNCCTTTAAATTCCNK" +
            ".KNCCCTTTTTCCNK." +
            "..KNNCCCTTCCNNK." +
            "...KNBBKKBBNK..." +
            "....KBFKKFBK...." +
            ".....KK..KK.....";

        var colors = new Dictionary<char, Color>
        {
            ['.'] = Color.Transparent,
            ['K'] = Color.FromArgb(26, 18, 14),
            ['H'] = Color.FromArgb(92, 46, 20),
            ['L'] = Color.FromArgb(138, 74, 34),
            ['S'] = Color.FromArgb(232, 184, 136),
            ['D'] = Color.FromArgb(196, 144, 104),
            ['E'] = Color.FromArgb(42, 24, 16),
            ['C'] = Color.FromArgb(48, 78, 122),
            ['N'] = Color.FromArgb(32, 52, 84),
            ['P'] = Color.FromArgb(78, 110, 150),
            ['T'] = Color.FromArgb(90, 118, 52),
            ['A'] = Color.FromArgb(122, 86, 42),
            ['B'] = Color.FromArgb(58, 36, 24),
            ['F'] = Color.FromArgb(107, 72, 48),
        };

        var bmp = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        for (var y = 0; y < NativeSize; y++)
        {
            for (var x = 0; x < NativeSize; x++)
            {
                bmp.SetPixel(x, y, colors[map[(y * NativeSize) + x]]);
            }
        }

        return bmp;
    }
}
