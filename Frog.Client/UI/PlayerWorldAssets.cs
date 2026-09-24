#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using Frog.Core.Gameplay;

namespace Frog.Client.UI;

/// <summary>
/// Eldiran CC0 32×32 top-down player (blue knight) plus original paperdoll overlays.
/// Idle + 4-dir walk composite each cell in <see cref="CompositeDrawOrder"/>:
/// body → tunic → armor → head → hat → weapon.
/// Combined <c>player.png</c> is south idle for fallback. Never Graal sheets.
/// Hat / weapon stay on that order for every walk frame (the up-facing blade
/// sits beside the body, still drawn last).
/// </summary>
internal static class PlayerWorldAssets
{
    public const string RelativePath = "Assets/World/player.png";
    public const string BodyRelativePath = "Assets/World/player-body.png";
    public const string HeadRelativePath = "Assets/World/player-head.png";
    public const string TunicRelativePath = "Assets/World/player-tunic.png";
    public const string ArmorRelativePath = "Assets/World/player-armor.png";
    public const string HatRelativePath = "Assets/World/player-hat.png";
    public const string WeaponRelativePath = "Assets/World/player-weapon.png";
    public const string WalkRelativePath = "Assets/World/player-walk.png";
    public const string WalkBodyRelativePath = "Assets/World/player-walk-body.png";
    public const string WalkHeadRelativePath = "Assets/World/player-walk-head.png";
    public const string WalkTunicRelativePath = "Assets/World/player-walk-tunic.png";
    public const string WalkArmorRelativePath = "Assets/World/player-walk-armor.png";
    public const string WalkHatRelativePath = "Assets/World/player-walk-hat.png";
    public const string WalkWeaponRelativePath = "Assets/World/player-walk-weapon.png";
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
        PlayerSpriteSlot.Hat,
        PlayerSpriteSlot.Weapon,
    ];

    private const string EmbeddedName = "Frog.Client.Assets.World.player.png";
    private const string EmbeddedBodyName = "Frog.Client.Assets.World.player-body.png";
    private const string EmbeddedHeadName = "Frog.Client.Assets.World.player-head.png";
    private const string EmbeddedTunicName = "Frog.Client.Assets.World.player-tunic.png";
    private const string EmbeddedArmorName = "Frog.Client.Assets.World.player-armor.png";
    private const string EmbeddedHatName = "Frog.Client.Assets.World.player-hat.png";
    private const string EmbeddedWeaponName = "Frog.Client.Assets.World.player-weapon.png";
    private const string EmbeddedWalkName = "Frog.Client.Assets.World.player-walk.png";
    private const string EmbeddedWalkBodyName = "Frog.Client.Assets.World.player-walk-body.png";
    private const string EmbeddedWalkHeadName = "Frog.Client.Assets.World.player-walk-head.png";
    private const string EmbeddedWalkTunicName = "Frog.Client.Assets.World.player-walk-tunic.png";
    private const string EmbeddedWalkArmorName = "Frog.Client.Assets.World.player-walk-armor.png";
    private const string EmbeddedWalkHatName = "Frog.Client.Assets.World.player-walk-hat.png";
    private const string EmbeddedWalkWeaponName = "Frog.Client.Assets.World.player-walk-weapon.png";

    private static readonly object Gate = new();
    private static Bitmap? _sprite;
    private static Bitmap?[,] _frames = new Bitmap?[PlayerWalkClock.SheetRows, PlayerWalkClock.SheetColumns];
    private static Bitmap?[,,]? _overlayFrames;
    private static Bitmap? _bodySheet;
    private static Bitmap? _headSheet;
    private static Bitmap? _tunicSheet;
    private static Bitmap? _armorSheet;
    private static Bitmap? _hatSheet;
    private static Bitmap? _weaponSheet;
    private static Bitmap?[]? _layerIcons;
    private static bool _resolved;
    private static string? _resolvedPath;

    static PlayerWorldAssets()
    {
        if (CompositeDrawOrder.Length != PaperdollDrawOrder.All.Length)
        {
            throw new InvalidOperationException("Paperdoll draw order length drifted.");
        }

        for (var i = 0; i < CompositeDrawOrder.Length; i++)
        {
            if ((byte)CompositeDrawOrder[i] != (byte)PaperdollDrawOrder.All[i])
            {
                throw new InvalidOperationException("PlayerSpriteSlot and PaperdollLayer draw order drifted.");
            }
        }
    }

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
    internal static Bitmap FrameFor(PlayerSpritePose pose, PaperdollOverlaySet appearance = default)
    {
        EnsureLoaded();
        var row = pose.SheetRow;
        var col = pose.SheetColumn;
        if (appearance.Equals(default(PaperdollOverlaySet)))
        {
            return _frames[row, col] ?? _sprite!;
        }

        lock (Gate)
        {
            _overlayFrames ??= new Bitmap?[16, PlayerWalkClock.SheetRows, PlayerWalkClock.SheetColumns];
            var index = OverlayIndex(appearance);
            var cached = _overlayFrames[index, row, col];
            if (cached is not null)
            {
                return cached;
            }

            cached = RenderCell(col, row, appearance);
            _overlayFrames[index, row, col] = cached;
            return cached;
        }
    }

    /// <summary>
    /// South-idle cell of one paperdoll layer (body, tunic, armor, head, hat, weapon).
    /// Same sheets as <see cref="FrameFor"/>. Empty bitmap when that overlay file is missing.
    /// </summary>
    internal static Bitmap LayerIcon(PlayerSpriteSlot slot)
    {
        EnsureLoaded();
        var index = (int)slot;
        if ((uint)index >= CompositeDrawOrder.Length)
        {
            return EmptyCell();
        }

        lock (Gate)
        {
            _layerIcons ??= new Bitmap?[CompositeDrawOrder.Length];
            var cached = _layerIcons[index];
            if (cached is not null)
            {
                return cached;
            }

            cached = CropIdleSouth(SheetFor(slot));
            _layerIcons[index] = cached;
            return cached;
        }
    }

    /// <summary>Feet / bottom-center of the sprite on <paramref name="centerXPx"/>, <paramref name="centerYPx"/>.</summary>
    internal static void DrawFeetAnchored(
        Graphics g,
        float centerXPx,
        float centerYPx,
        bool other,
        PlayerSpritePose pose = default,
        PaperdollOverlaySet appearance = default)
    {
        ArgumentNullException.ThrowIfNull(g);
        var sprite = FrameFor(pose, appearance);
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

    private static int OverlayIndex(PaperdollOverlaySet set)
    {
        var index = 0;
        if (set.Tunic)
        {
            index |= 1;
        }

        if (set.Armor)
        {
            index |= 2;
        }

        if (set.Hat)
        {
            index |= 4;
        }

        if (set.Weapon)
        {
            index |= 8;
        }

        return index;
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

            _bodySheet = LoadWalkOrIdle(WalkBodyRelativePath, EmbeddedWalkBodyName, BodyRelativePath, EmbeddedBodyName);
            _headSheet = LoadWalkOrIdle(WalkHeadRelativePath, EmbeddedWalkHeadName, HeadRelativePath, EmbeddedHeadName);
            _tunicSheet = LoadWalkOrIdle(WalkTunicRelativePath, EmbeddedWalkTunicName, TunicRelativePath, EmbeddedTunicName);
            _armorSheet = LoadWalkOrIdle(WalkArmorRelativePath, EmbeddedWalkArmorName, ArmorRelativePath, EmbeddedArmorName);
            _hatSheet = LoadWalkOrIdle(WalkHatRelativePath, EmbeddedWalkHatName, HatRelativePath, EmbeddedHatName);
            _weaponSheet = LoadWalkOrIdle(WalkWeaponRelativePath, EmbeddedWalkWeaponName, WeaponRelativePath, EmbeddedWeaponName);

            if (_bodySheet is { Width: WalkSheetWidth } bodySheet
                && _headSheet is { Width: WalkSheetWidth } headSheet)
            {
                for (var row = 0; row < PlayerWalkClock.SheetRows; row++)
                {
                    for (var col = 0; col < PlayerWalkClock.SheetColumns; col++)
                    {
                        _frames[row, col] = ComposeCell(bodySheet, headSheet, col * NativeSize, row * NativeSize);
                    }
                }
            }
            else
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

    private static Bitmap? LoadWalkOrIdle(string walkPath, string walkEmbedded, string idlePath, string idleEmbedded)
        => TryLoadNamedPng(walkPath, walkEmbedded, WalkSheetWidth, WalkSheetHeight)
            ?? TryLoadNamedPng(idlePath, idleEmbedded, NativeSize, NativeSize);

    private static Bitmap RenderCell(int col, int row, PaperdollOverlaySet set)
    {
        if (_bodySheet is null || _headSheet is null)
        {
            return _frames[row, col] ?? _sprite ?? CreateFallbackRaster();
        }

        var composed = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(composed);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);
        foreach (var slot in CompositeDrawOrder)
        {
            if (!set.IsLayerVisible((PaperdollLayer)(byte)slot))
            {
                continue;
            }

            var sheet = SheetFor(slot);
            if (sheet is null)
            {
                continue;
            }

            var origin = CellOrigin(sheet, col, row);
            g.DrawImage(
                sheet,
                new Rectangle(0, 0, NativeSize, NativeSize),
                new Rectangle(origin.X, origin.Y, NativeSize, NativeSize),
                GraphicsUnit.Pixel);
        }

        return composed;
    }

    private static Bitmap? SheetFor(PlayerSpriteSlot slot) => slot switch
    {
        PlayerSpriteSlot.Body => _bodySheet,
        PlayerSpriteSlot.Tunic => _tunicSheet,
        PlayerSpriteSlot.Armor => _armorSheet,
        PlayerSpriteSlot.Head => _headSheet,
        PlayerSpriteSlot.Hat => _hatSheet,
        PlayerSpriteSlot.Weapon => _weaponSheet,
        _ => null,
    };

    private static Bitmap CropIdleSouth(Bitmap? sheet)
    {
        var cell = EmptyCell();
        if (sheet is null)
        {
            return cell;
        }

        using var g = Graphics.FromImage(cell);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);
        var col = sheet.Width >= NativeSize * (PlayerWalkClock.IdleColumn + 1)
            ? PlayerWalkClock.IdleColumn
            : 0;
        var row = sheet.Height >= WalkSheetHeight ? PlayerSpritePose.IdleDown.SheetRow : 0;
        var origin = CellOrigin(sheet, col, row);
        g.DrawImage(
            sheet,
            new Rectangle(0, 0, NativeSize, NativeSize),
            new Rectangle(origin.X, origin.Y, NativeSize, NativeSize),
            GraphicsUnit.Pixel);
        return cell;
    }

    private static Bitmap EmptyCell() => new(NativeSize, NativeSize, PixelFormat.Format32bppArgb);

    private static Point CellOrigin(Bitmap sheet, int col, int row)
    {
        if (sheet.Width < NativeSize * (col + 1) || sheet.Height < NativeSize * (row + 1))
        {
            return new Point(0, 0);
        }

        return new Point(col * NativeSize, row * NativeSize);
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
        _bodySheet ??= TryLoadNamedPng(BodyRelativePath, EmbeddedBodyName, NativeSize, NativeSize);
        _headSheet ??= TryLoadNamedPng(HeadRelativePath, EmbeddedHeadName, NativeSize, NativeSize);
        if (_bodySheet is null || _headSheet is null)
        {
            return null;
        }

        return ComposeCell(_bodySheet, _headSheet, 0, 0);
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
        // Draw order when nothing is equipped: Body → Head. Overlays stay off.
        g.DrawImage(body, dst, src, GraphicsUnit.Pixel);
        g.DrawImage(head, dst, src, GraphicsUnit.Pixel);
        return composed;
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
