#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Frog.Client.UI;

/// <summary>
/// Catalogue Kenney + game-icons sous <c>Assets/Ui</c>. Charge une fois, clone pour les
/// contrôles (WinForms dispose Image / BackgroundImage). Pas de second HUD.
/// </summary>
public static class UiPackAssets
{
    public const string RelativeRoot = "Assets/Ui";

    private static readonly object Gate = new();
    private static bool _loaded;
    private static string? _resolvedRoot;

    private static Image? _framePanel;
    private static Image? _frameInset;
    private static Image? _slot;
    private static Image? _slotPressed;
    private static Image? _menuPill;
    private static Image? _cta;
    private static Image? _ctaPressed;
    private static Image? _arrowLeft;
    private static Image? _arrowRight;
    private static Image? _barBackLeft;
    private static Image? _barBackMid;
    private static Image? _barBackRight;
    private static Image? _barRedLeft;
    private static Image? _barRedMid;
    private static Image? _barRedRight;
    private static Image? _barBlueLeft;
    private static Image? _barBlueMid;
    private static Image? _barBlueRight;
    private static Image? _iconWalk;
    private static Image? _iconBackpack;
    private static Image? _iconScroll;
    private static Image? _iconMap;
    private static Image? _iconCog;
    private static Image? _iconSword;
    private static Image? _iconSpell;
    private static Image? _iconHand;

    public static string? ResolvedRootForTest
    {
        get
        {
            EnsureLoaded();
            return _resolvedRoot;
        }
    }

    public static bool HasFramePanel
    {
        get
        {
            EnsureLoaded();
            return _framePanel is not null;
        }
    }

    public static bool HasFrameInset
    {
        get
        {
            EnsureLoaded();
            return _frameInset is not null;
        }
    }

    public static bool HasSlot
    {
        get
        {
            EnsureLoaded();
            return _slot is not null;
        }
    }

    public static bool HasMenuPill
    {
        get
        {
            EnsureLoaded();
            return _menuPill is not null;
        }
    }

    public static bool HasCta
    {
        get
        {
            EnsureLoaded();
            return _cta is not null;
        }
    }

    public static bool HasBarBack
    {
        get
        {
            EnsureLoaded();
            return _barBackLeft is not null && _barBackMid is not null && _barBackRight is not null;
        }
    }

    public static bool HasBarHp
    {
        get
        {
            EnsureLoaded();
            return _barRedLeft is not null && _barRedMid is not null && _barRedRight is not null;
        }
    }

    public static bool HasBarMp
    {
        get
        {
            EnsureLoaded();
            return _barBlueLeft is not null && _barBlueMid is not null && _barBlueRight is not null;
        }
    }

    public static bool TryGetFramePanel(out Image image) => TryGet(ref _framePanel, out image);

    public static bool TryGetFrameInset(out Image image) => TryGet(ref _frameInset, out image);

    public static bool TryGetBarBack(out Image left, out Image mid, out Image right)
    {
        EnsureLoaded();
        if (_barBackLeft is null || _barBackMid is null || _barBackRight is null)
        {
            left = mid = right = null!;
            return false;
        }

        left = _barBackLeft;
        mid = _barBackMid;
        right = _barBackRight;
        return true;
    }

    public static bool TryGetBarFill(bool hp, out Image left, out Image mid, out Image right)
    {
        EnsureLoaded();
        var l = hp ? _barRedLeft : _barBlueLeft;
        var m = hp ? _barRedMid : _barBlueMid;
        var r = hp ? _barRedRight : _barBlueRight;
        if (l is null || m is null || r is null)
        {
            left = mid = right = null!;
            return false;
        }

        left = l;
        mid = m;
        right = r;
        return true;
    }

    public static Image? CloneSlot()
    {
        EnsureLoaded();
        return Clone(_slot);
    }

    public static Image? CloneMenuPill()
    {
        EnsureLoaded();
        return Clone(_menuPill);
    }

    public static Image? CloneCta()
    {
        EnsureLoaded();
        return Clone(_cta);
    }

    public static Image? CloneMenuIcon(HudMenuCommand command)
    {
        EnsureLoaded();
        var source = command switch
        {
            HudMenuCommand.Character => _iconWalk,
            HudMenuCommand.Inventory => _iconBackpack,
            HudMenuCommand.Quests => _iconScroll,
            HudMenuCommand.Map => _iconMap,
            HudMenuCommand.Options => _iconCog,
            _ => null,
        };
        return CloneTinted(source, new Size(18, 18), gold: true);
    }

    public static Image? CloneHotbarIcon(int slotIndex)
    {
        EnsureLoaded();
        var source = slotIndex switch
        {
            0 => _iconSword,
            1 => _iconSpell,
            2 => _iconHand,
            _ => null,
        };
        return CloneTinted(source, new Size(18, 18), gold: true);
    }

    public static bool TryResolveRoot(out string root)
    {
        foreach (var candidate in RootCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "frames", "panel_brown.png")))
            {
                root = candidate;
                return true;
            }
        }

        root = string.Empty;
        return false;
    }

    internal static IReadOnlyList<string> ExpectedRelativeFiles { get; } =
    [
        "frames/panel_brown.png",
        "frames/panelInset_brown.png",
        "slots/buttonSquare_brown.png",
        "slots/buttonSquare_brown_pressed.png",
        "menu/buttonRound_brown.png",
        "bars/barRed_horizontalLeft.png",
        "bars/barRed_horizontalMid.png",
        "bars/barRed_horizontalRight.png",
        "bars/barBlue_horizontalLeft.png",
        "bars/barBlue_horizontalBlue.png",
        "bars/barBlue_horizontalRight.png",
        "bars/barBack_horizontalLeft.png",
        "bars/barBack_horizontalMid.png",
        "bars/barBack_horizontalRight.png",
        "chrome/buttonLong_brown.png",
        "chrome/arrowBrown_left.png",
        "chrome/arrowBrown_right.png",
        "icons/menu/walk.png",
        "icons/menu/backpack.png",
        "icons/menu/scroll-unfurled.png",
        "icons/menu/treasure-map.png",
        "icons/menu/cog.png",
        "icons/hotbar/broadsword.png",
        "icons/hotbar/fire-spell-cast.png",
        "icons/hotbar/hand.png",
        "CREDITS.md",
    ];

    private static bool TryGet(ref Image? cached, out Image image)
    {
        EnsureLoaded();
        if (cached is null)
        {
            image = null!;
            return false;
        }

        image = cached;
        return true;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (Gate)
        {
            if (_loaded)
            {
                return;
            }

            if (TryResolveRoot(out var root))
            {
                _resolvedRoot = root;
                _framePanel = Load(root, "frames/panel_brown.png");
                _frameInset = Load(root, "frames/panelInset_brown.png");
                _slot = Load(root, "slots/buttonSquare_brown.png");
                _slotPressed = Load(root, "slots/buttonSquare_brown_pressed.png");
                _menuPill = Load(root, "menu/buttonRound_brown.png");
                _cta = Load(root, "chrome/buttonLong_brown.png");
                _ctaPressed = Load(root, "chrome/buttonLong_brown_pressed.png");
                _arrowLeft = Load(root, "chrome/arrowBrown_left.png");
                _arrowRight = Load(root, "chrome/arrowBrown_right.png");
                _barBackLeft = Load(root, "bars/barBack_horizontalLeft.png");
                _barBackMid = Load(root, "bars/barBack_horizontalMid.png");
                _barBackRight = Load(root, "bars/barBack_horizontalRight.png");
                _barRedLeft = Load(root, "bars/barRed_horizontalLeft.png");
                _barRedMid = Load(root, "bars/barRed_horizontalMid.png");
                _barRedRight = Load(root, "bars/barRed_horizontalRight.png");
                _barBlueLeft = Load(root, "bars/barBlue_horizontalLeft.png");
                _barBlueMid = Load(root, "bars/barBlue_horizontalBlue.png");
                _barBlueRight = Load(root, "bars/barBlue_horizontalRight.png");
                _iconWalk = Load(root, "icons/menu/walk.png");
                _iconBackpack = Load(root, "icons/menu/backpack.png");
                _iconScroll = Load(root, "icons/menu/scroll-unfurled.png");
                _iconMap = Load(root, "icons/menu/treasure-map.png");
                _iconCog = Load(root, "icons/menu/cog.png");
                _iconSword = Load(root, "icons/hotbar/broadsword.png");
                _iconSpell = Load(root, "icons/hotbar/fire-spell-cast.png");
                _iconHand = Load(root, "icons/hotbar/hand.png");
            }

            _loaded = true;
        }
    }

    private static Image? Load(string root, string relative)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var tmp = new Bitmap(path);
            return new Bitmap(tmp);
        }
        catch
        {
            return null;
        }
    }

    private static Image? Clone(Image? source)
    {
        if (source is null)
        {
            return null;
        }

        return new Bitmap(source);
    }

    private static Image? CloneTinted(Image? source, Size size, bool gold)
    {
        if (source is null || size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        using var attrs = gold ? UiTheme.CreateGoldTintAttributes() : UiTheme.CreatePanelTintAttributes();
        return UiTheme.TintCopy(source, size, attrs);
    }

    private static IEnumerable<string> RootCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Ui");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, "Assets", "Ui");
            yield return Path.Combine(dir.FullName, "Frog.Client", "Assets", "Ui");
            dir = dir.Parent;
        }
    }

    // Keep pressed/arrow caches referenced so the loader is the single source of truth.
    internal static bool HasPressedSlotForTest
    {
        get
        {
            EnsureLoaded();
            return _slotPressed is not null;
        }
    }

    internal static bool HasDialogueArrowsForTest
    {
        get
        {
            EnsureLoaded();
            return _arrowLeft is not null && _arrowRight is not null && _ctaPressed is not null;
        }
    }
}

/// <summary>Découpe 9 / 3 slices Kenney — pixels nearest, hors passe tiles.</summary>
public static class UiPackDraw
{
    public const int PanelNineSliceBorder = 10;

    public static void NineSlice(Graphics graphics, Image image, Rectangle dest, int border, ImageAttributes? attrs)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(image);
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return;
        }

        var prev = graphics.InterpolationMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        var b = Math.Clamp(border, 1, Math.Min(image.Width, image.Height) / 3);
        var dw = dest.Width;
        var dh = dest.Height;
        var sw = image.Width;
        var sh = image.Height;
        var db = Math.Min(b, Math.Min(dw, dh) / 2);

        Draw(graphics, image, new Rectangle(dest.X, dest.Y, db, db), 0, 0, b, b, attrs);
        Draw(graphics, image, new Rectangle(dest.X + dw - db, dest.Y, db, db), sw - b, 0, b, b, attrs);
        Draw(graphics, image, new Rectangle(dest.X, dest.Y + dh - db, db, db), 0, sh - b, b, b, attrs);
        Draw(graphics, image, new Rectangle(dest.X + dw - db, dest.Y + dh - db, db, db), sw - b, sh - b, b, b, attrs);

        var midW = dw - (db * 2);
        var midH = dh - (db * 2);
        var srcMidW = sw - (b * 2);
        var srcMidH = sh - (b * 2);
        if (midW > 0)
        {
            Draw(graphics, image, new Rectangle(dest.X + db, dest.Y, midW, db), b, 0, srcMidW, b, attrs);
            Draw(graphics, image, new Rectangle(dest.X + db, dest.Y + dh - db, midW, db), b, sh - b, srcMidW, b, attrs);
        }

        if (midH > 0)
        {
            Draw(graphics, image, new Rectangle(dest.X, dest.Y + db, db, midH), 0, b, b, srcMidH, attrs);
            Draw(graphics, image, new Rectangle(dest.X + dw - db, dest.Y + db, db, midH), sw - b, b, b, srcMidH, attrs);
        }

        if (midW > 0 && midH > 0)
        {
            Draw(graphics, image, new Rectangle(dest.X + db, dest.Y + db, midW, midH), b, b, srcMidW, srcMidH, attrs);
        }

        graphics.InterpolationMode = prev;
    }

    public static void ThreeSliceHorizontal(Graphics graphics, Image left, Image mid, Image right, Rectangle dest, ImageAttributes? attrs)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(mid);
        ArgumentNullException.ThrowIfNull(right);
        if (dest.Width <= 0 || dest.Height <= 0)
        {
            return;
        }

        var prev = graphics.InterpolationMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        var cap = Math.Max(1, Math.Min(left.Width, dest.Width / 2));
        if (dest.Width <= cap * 2)
        {
            Draw(graphics, left, new Rectangle(dest.X, dest.Y, dest.Width, dest.Height), 0, 0, left.Width, left.Height, attrs);
            graphics.InterpolationMode = prev;
            return;
        }

        Draw(graphics, left, new Rectangle(dest.X, dest.Y, cap, dest.Height), 0, 0, left.Width, left.Height, attrs);
        Draw(graphics, right, new Rectangle(dest.X + dest.Width - cap, dest.Y, cap, dest.Height), 0, 0, right.Width, right.Height, attrs);
        Draw(graphics, mid, new Rectangle(dest.X + cap, dest.Y, dest.Width - (cap * 2), dest.Height), 0, 0, mid.Width, mid.Height, attrs);
        graphics.InterpolationMode = prev;
    }

    private static void Draw(Graphics graphics, Image image, Rectangle dest, int srcX, int srcY, int srcW, int srcH, ImageAttributes? attrs)
    {
        if (dest.Width <= 0 || dest.Height <= 0 || srcW <= 0 || srcH <= 0)
        {
            return;
        }

        if (attrs is null)
        {
            graphics.DrawImage(image, dest, srcX, srcY, srcW, srcH, GraphicsUnit.Pixel);
            return;
        }

        graphics.DrawImage(image, dest, srcX, srcY, srcW, srcH, GraphicsUnit.Pixel, attrs);
    }
}
