#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using Frog.Core.Gameplay;

namespace Frog.Client.UI;

/// <summary>
/// Eldiran CC0 32×32 top-down player (blue knight). Idle + 4-dir walk composite
/// <c>player-walk-body.png</c> then <c>player-walk-head.png</c> (tunic/armor/weapon empty).
/// Combined <c>player.png</c> is south idle for fallback. Never Graal sheets.
/// </summary>
internal static class PlayerWorldAssets
{
    public const string RelativePath = "Assets/World/player.png";
    public const string BodyRelativePath = "Assets/World/player-body.png";
    public const string HeadRelativePath = "Assets/World/player-head.png";
    public const string WalkRelativePath = "Assets/World/player-walk.png";
    public const string WalkBodyRelativePath = "Assets/World/player-walk-body.png";
    public const string WalkHeadRelativePath = "Assets/World/player-walk-head.png";
    public const int NativeSize = 32;
    public const int DrawScale = 1;
    public const int HeadRows = 14;
    public const int WalkSheetWidth = PlayerWalkClock.SheetColumns * NativeSize;
    public const int WalkSheetHeight = PlayerWalkClock.SheetRows * NativeSize;

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
    private const string EmbeddedWalkName = "Frog.Client.Assets.World.player-walk.png";
    private const string EmbeddedWalkBodyName = "Frog.Client.Assets.World.player-walk-body.png";
    private const string EmbeddedWalkHeadName = "Frog.Client.Assets.World.player-walk-head.png";

    private static readonly object Gate = new();
    private static Bitmap? _sprite;
    private static Bitmap?[,] _frames = new Bitmap?[PlayerWalkClock.SheetRows, PlayerWalkClock.SheetColumns];
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

    /// <summary>Composed 32×32 cell for a pose (south idle when the walk sheet is missing).</summary>
    internal static Bitmap FrameFor(PlayerSpritePose pose)
    {
        EnsureLoaded();
        var row = pose.SheetRow;
        var col = pose.SheetColumn;
        return _frames[row, col] ?? _sprite!;
    }

    /// <summary>Feet / bottom-center of the sprite on <paramref name="centerXPx"/>, <paramref name="centerYPx"/>.</summary>
    internal static void DrawFeetAnchored(Graphics g, float centerXPx, float centerYPx, bool other, PlayerSpritePose pose = default)
    {
        ArgumentNullException.ThrowIfNull(g);
        var sprite = FrameFor(pose);
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

            if (!TryLoadWalkFrames())
            {
                var idle = TryComposeBodyAndHead()
                    ?? TryLoadNamedPng(RelativePath, EmbeddedName, NativeSize, NativeSize)
                    ?? CreateFallbackRaster();
                FillAllFrames(idle);
            }

            _sprite = _frames[0, PlayerWalkClock.IdleColumn] ?? CreateFallbackRaster();
            _resolved = true;
        }
    }

    private static bool TryLoadWalkFrames()
    {
        using var bodySheet = TryLoadNamedPng(WalkBodyRelativePath, EmbeddedWalkBodyName, WalkSheetWidth, WalkSheetHeight);
        using var headSheet = TryLoadNamedPng(WalkHeadRelativePath, EmbeddedWalkHeadName, WalkSheetWidth, WalkSheetHeight);
        if (bodySheet is not null && headSheet is not null)
        {
            for (var row = 0; row < PlayerWalkClock.SheetRows; row++)
            {
                for (var col = 0; col < PlayerWalkClock.SheetColumns; col++)
                {
                    _frames[row, col] = ComposeCell(bodySheet, headSheet, col * NativeSize, row * NativeSize);
                }
            }

            return true;
        }

        using var walk = TryLoadNamedPng(WalkRelativePath, EmbeddedWalkName, WalkSheetWidth, WalkSheetHeight);
        if (walk is null)
        {
            return false;
        }

        for (var row = 0; row < PlayerWalkClock.SheetRows; row++)
        {
            for (var col = 0; col < PlayerWalkClock.SheetColumns; col++)
            {
                _frames[row, col] = CropCell(walk, col * NativeSize, row * NativeSize);
            }
        }

        return true;
    }

    private static void FillAllFrames(Bitmap idle)
    {
        for (var row = 0; row < PlayerWalkClock.SheetRows; row++)
        {
            for (var col = 0; col < PlayerWalkClock.SheetColumns; col++)
            {
                _frames[row, col] = idle;
            }
        }
    }

    private static Bitmap? TryComposeBodyAndHead()
    {
        using var body = TryLoadNamedPng(BodyRelativePath, EmbeddedBodyName, NativeSize, NativeSize);
        using var head = TryLoadNamedPng(HeadRelativePath, EmbeddedHeadName, NativeSize, NativeSize);
        if (body is null || head is null)
        {
            return null;
        }

        return ComposeCell(body, head, 0, 0);
    }

    private static Bitmap ComposeCell(Bitmap body, Bitmap head, int srcX, int srcY)
    {
        var composed = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(composed);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);
        var src = new Rectangle(srcX, srcY, NativeSize, NativeSize);
        var dst = new Rectangle(0, 0, NativeSize, NativeSize);
        // Draw order: Body → (Tunic/Armor empty) → Head → (Weapon empty).
        g.DrawImage(body, dst, src, GraphicsUnit.Pixel);
        g.DrawImage(head, dst, src, GraphicsUnit.Pixel);
        return composed;
    }

    private static Bitmap CropCell(Bitmap sheet, int srcX, int srcY)
    {
        var cell = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(cell);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(sheet, new Rectangle(0, 0, NativeSize, NativeSize),
            new Rectangle(srcX, srcY, NativeSize, NativeSize), GraphicsUnit.Pixel);
        return cell;
    }

    private static Bitmap? TryLoadNamedPng(string relativePath, string embeddedName, int expectW, int expectH)
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
                if (tmp.Width != expectW || tmp.Height != expectH)
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

        return TryLoadEmbedded(embeddedName, expectW, expectH);
    }

    private static Bitmap? TryLoadEmbedded(string embeddedName, int expectW, int expectH)
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
            if (tmp.Width != expectW || tmp.Height != expectH)
            {
                return null;
            }

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
