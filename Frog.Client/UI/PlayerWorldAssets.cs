#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace Frog.Client.UI;

/// <summary>
/// Eldiran CC0 32×32 top-down player (blue knight, south idle). v1 composites
/// <c>player-body.png</c> then <c>player-head.png</c> (tunic/armor/weapon empty).
/// Combined <c>player.png</c> is the same idle for fallback. Never Graal sheets.
/// </summary>
internal static class PlayerWorldAssets
{
    public const string RelativePath = "Assets/World/player.png";
    public const string BodyRelativePath = "Assets/World/player-body.png";
    public const string HeadRelativePath = "Assets/World/player-head.png";
    public const int NativeSize = 32;
    public const int DrawScale = 1;
    public const int HeadRows = 14;

    internal static readonly PlayerSpriteSlot[] CompositeDrawOrder =
    [
        PlayerSpriteSlot.Body,
        PlayerSpriteSlot.Tunic,
        PlayerSpriteSlot.Armor,
        PlayerSpriteSlot.Head,
        PlayerSpriteSlot.Weapon,
    ];

    private const string EmbeddedName = "Frog.Client.Assets.World.player.png";
    private const string EmbeddedBodyName = "Frog.Client.Assets.World.player-body.png";
    private const string EmbeddedHeadName = "Frog.Client.Assets.World.player-head.png";

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

    /// <summary>Feet / bottom-center of the sprite on <paramref name="centerXPx"/>, <paramref name="centerYPx"/>.</summary>
    internal static void DrawFeetAnchored(Graphics g, float centerXPx, float centerYPx, bool other)
    {
        ArgumentNullException.ThrowIfNull(g);
        var sprite = Sprite;
        var dw = sprite.Width * DrawScale;
        var dh = sprite.Height * DrawScale;
        var dest = new Rectangle(
            (int)MathF.Round(centerXPx - dw / 2f),
            (int)MathF.Round(centerYPx - dh + 1f),
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

            _sprite = TryComposeBodyAndHead()
                ?? TryLoadNamedPng(RelativePath, EmbeddedName)
                ?? CreateFallbackRaster();
            _resolved = true;
        }
    }

    private static Bitmap? TryComposeBodyAndHead()
    {
        using var body = TryLoadNamedPng(BodyRelativePath, EmbeddedBodyName);
        using var head = TryLoadNamedPng(HeadRelativePath, EmbeddedHeadName);
        if (body is null || head is null)
        {
            return null;
        }

        var composed = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(composed);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);
        // Draw order: Body → (Tunic/Armor empty) → Head → (Weapon empty).
        g.DrawImage(body, new Rectangle(0, 0, NativeSize, NativeSize),
            new Rectangle(0, 0, body.Width, body.Height), GraphicsUnit.Pixel);
        g.DrawImage(head, new Rectangle(0, 0, NativeSize, NativeSize),
            new Rectangle(0, 0, head.Width, head.Height), GraphicsUnit.Pixel);
        return composed;
    }

    private static Bitmap? TryLoadNamedPng(string relativePath, string embeddedName)
    {
        foreach (var path in FileCandidates(relativePath))
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

                if (relativePath == RelativePath)
                {
                    _resolvedPath = path;
                }

                return new Bitmap(tmp);
            }
            catch
            {
                // try next candidate
            }
        }

        return TryLoadEmbedded(embeddedName);
    }

    private static Bitmap? TryLoadEmbedded(string embeddedName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(embeddedName);
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

    private static IEnumerable<string> FileCandidates(string relativePath)
    {
        var parts = relativePath.Split('/');
        yield return Path.Combine(AppContext.BaseDirectory, Path.Combine(parts));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, Path.Combine(parts));
            yield return Path.Combine(dir.FullName, "Frog.Client", Path.Combine(parts));
            dir = dir.Parent;
        }
    }

    /// <summary>Same Eldiran cell as <c>tools/generate-player-sprite.py</c> if the PNG is missing.</summary>
    private static Bitmap CreateFallbackRaster()
    {
        string[] map =
        [
            "..........KKKABBBBAKKK..........",
            ".........KABBBBBBBBBBAK.........",
            "........KABBAAAAAAAABBAK........",
            "........KBAACDDDDDDCAABK........",
            ".......KBACDDBBBBBBDDCABK.......",
            ".......KACDABBKBBKBBADCAK.......",
            ".......KADCAKBKBBKBKACDAK.......",
            ".......KDCABKBKBBKBKBACDK.......",
            ".......KDCABKBKBBKBKBACDK.......",
            ".......KCDCABBBBBBBBACDCK.......",
            ".......KAKDDDDDDDDDDDDKAK.......",
            ".......KAKEEEEEEEEEEEEKAK.......",
            ".......KCKEEEEEEEEEEEEKCK.......",
            "........KDKEEEEEEEEEEKDK........",
            ".....KKKKDCKEEEEEEEEKCDKKKK.....",
            "....KDBBACDCKKKKKKKKCDCABBDK....",
            "....KCAAACDCAAACCAAACDCAAACK....",
            "....KFCAADCABBAAAABBACDAACFK....",
            "....KFGDCDCABBBBBBBBACDCDGFK....",
            "...KCDFGKDCAABBAABBAACDKGFDCK...",
            "...KACDDFKDCAAAAAAAACDKFDDCAK...",
            "...KAAACKFKDCCACCACCDKFKCAAAK...",
            "...KBAAAKFKHHJJLLJJHHKFKAAABK...",
            "....KBAK.KCDGDCAACDGDCK.KABK....",
            ".....KKK.KDFFFDCCDFFFDK.KKK.....",
            ".........KGFFFGKKGFFFGK.........",
            ".........KDDFGM..MGFDDK.........",
            "........KAACDDD..DDDCAAK........",
            "........KACCCCK..KCCCCAK........",
            "........KCCCCDK..KDCCCCK........",
            "........KDDDDDK..KDDDDDK........",
            "........KKKKKKK..KKKKKKK........",
        ];

        var colors = new Dictionary<char, Color>
        {
            ['.'] = Color.Transparent,
            ['K'] = Color.FromArgb(0, 0, 0),
            ['A'] = Color.FromArgb(127, 146, 255),
            ['B'] = Color.FromArgb(175, 190, 255),
            ['C'] = Color.FromArgb(102, 117, 204),
            ['D'] = Color.FromArgb(76, 87, 153),
            ['E'] = Color.FromArgb(255, 229, 229),
            ['F'] = Color.FromArgb(255, 255, 255),
            ['G'] = Color.FromArgb(204, 204, 204),
            ['H'] = Color.FromArgb(127, 0, 0),
            ['J'] = Color.FromArgb(175, 0, 0),
            ['L'] = Color.FromArgb(26, 18, 14),
            ['M'] = Color.FromArgb(153, 153, 153),
        };

        var bmp = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        for (var y = 0; y < NativeSize; y++)
        {
            for (var x = 0; x < NativeSize; x++)
            {
                bmp.SetPixel(x, y, colors[map[y][x]]);
            }
        }

        return bmp;
    }
}
