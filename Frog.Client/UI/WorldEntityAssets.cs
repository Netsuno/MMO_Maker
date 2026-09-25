#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using Frog.Core.Gameplay;

namespace Frog.Client.UI;

/// <summary>
/// NPC / monster walk sheets (96×128). NPC = Eldiran row-9 villager (CC0, vendored).
/// Monster = in-repo procedural slime (CC0). Feet-anchored, nearest ×1. Never Graal.
/// </summary>
internal static class WorldEntityAssets
{
    public const string NpcRelativePath = "Assets/World/npc.png";
    public const string NpcWalkRelativePath = "Assets/World/npc-walk.png";
    public const string NpcAttackRelativePath = "Assets/World/npc-attack.png";
    public const string NpcDeathRelativePath = "Assets/World/npc-death.png";
    public const string MonsterRelativePath = "Assets/World/monster.png";
    public const string MonsterWalkRelativePath = "Assets/World/monster-walk.png";
    public const string MonsterAttackRelativePath = "Assets/World/monster-attack.png";
    public const string MonsterDeathRelativePath = "Assets/World/monster-death.png";
    public const int NativeSize = 32;
    public const int DrawScale = 1;
    public const int WalkSheetWidth = WalkClock.SheetColumns * NativeSize;
    public const int WalkSheetHeight = WalkClock.SheetRows * NativeSize;

    private const string EmbeddedNpcName = "Frog.Client.Assets.World.npc.png";
    private const string EmbeddedNpcWalkName = "Frog.Client.Assets.World.npc-walk.png";
    private const string EmbeddedNpcAttackName = "Frog.Client.Assets.World.npc-attack.png";
    private const string EmbeddedNpcDeathName = "Frog.Client.Assets.World.npc-death.png";
    private const string EmbeddedMonsterName = "Frog.Client.Assets.World.monster.png";
    private const string EmbeddedMonsterWalkName = "Frog.Client.Assets.World.monster-walk.png";
    private const string EmbeddedMonsterAttackName = "Frog.Client.Assets.World.monster-attack.png";
    private const string EmbeddedMonsterDeathName = "Frog.Client.Assets.World.monster-death.png";

    private static readonly object Gate = new();
    private static bool _resolved;
    private static readonly Bitmap?[,] _npcFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static readonly Bitmap?[,] _npcAttackFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static readonly Bitmap?[,] _npcDeathFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static readonly Bitmap?[,] _monsterFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static readonly Bitmap?[,] _monsterAttackFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static readonly Bitmap?[,] _monsterDeathFrames = new Bitmap?[WalkClock.SheetRows, WalkClock.SheetColumns];
    private static Bitmap? _npcIdle;
    private static Bitmap? _monsterIdle;

    internal static int DrawnSizePixels => NativeSize * DrawScale;

    internal static Bitmap FrameFor(WorldEntityKind kind, WorldSpritePose pose)
    {
        EnsureLoaded();
        var walk = kind == WorldEntityKind.Monster ? _monsterFrames : _npcFrames;
        var actionFrames = pose.Action switch
        {
            SpriteAction.Attack => kind == WorldEntityKind.Monster ? _monsterAttackFrames : _npcAttackFrames,
            SpriteAction.Death => kind == WorldEntityKind.Monster ? _monsterDeathFrames : _npcDeathFrames,
            _ => walk,
        };
        var fallback = kind == WorldEntityKind.Monster ? _monsterIdle : _npcIdle;
        var row = pose.SheetRow;
        var col = pose.SheetColumn;
        return actionFrames[row, col] ?? walk[row, col] ?? fallback!;
    }

    /// <summary>Feet / bottom-center of the sprite on <paramref name="centerXPx"/>, <paramref name="centerYPx"/>.</summary>
    internal static void DrawFeetAnchored(
        Graphics g,
        float centerXPx,
        float centerYPx,
        WorldEntityKind kind,
        WorldSpritePose pose = default)
    {
        ArgumentNullException.ThrowIfNull(g);
        var sprite = FrameFor(kind, pose);
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
            g.DrawImage(sprite, dest, src, GraphicsUnit.Pixel);
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

            LoadKind(
                NpcWalkRelativePath,
                EmbeddedNpcWalkName,
                NpcRelativePath,
                EmbeddedNpcName,
                _npcFrames,
                CreateNpcFallback,
                out _npcIdle);
            LoadKind(
                MonsterWalkRelativePath,
                EmbeddedMonsterWalkName,
                MonsterRelativePath,
                EmbeddedMonsterName,
                _monsterFrames,
                CreateMonsterFallback,
                out _monsterIdle);
            LoadActionSheet(NpcAttackRelativePath, EmbeddedNpcAttackName, _npcAttackFrames);
            LoadActionSheet(NpcDeathRelativePath, EmbeddedNpcDeathName, _npcDeathFrames);
            LoadActionSheet(MonsterAttackRelativePath, EmbeddedMonsterAttackName, _monsterAttackFrames);
            LoadActionSheet(MonsterDeathRelativePath, EmbeddedMonsterDeathName, _monsterDeathFrames);
            _resolved = true;
        }
    }

    private static void LoadKind(
        string walkRel,
        string walkEmbedded,
        string idleRel,
        string idleEmbedded,
        Bitmap?[,] frames,
        Func<Bitmap> fallbackFactory,
        out Bitmap idle)
    {
        using var walk = TryLoadNamedPng(walkRel, walkEmbedded, WalkSheetWidth, WalkSheetHeight);
        if (walk is not null)
        {
            for (var row = 0; row < WalkClock.SheetRows; row++)
            {
                for (var col = 0; col < WalkClock.SheetColumns; col++)
                {
                    frames[row, col] = CropCell(walk, col * NativeSize, row * NativeSize);
                }
            }

            idle = frames[0, WalkClock.IdleColumn] ?? fallbackFactory();
            return;
        }

        var single = TryLoadNamedPng(idleRel, idleEmbedded, NativeSize, NativeSize) ?? fallbackFactory();
        for (var row = 0; row < WalkClock.SheetRows; row++)
        {
            for (var col = 0; col < WalkClock.SheetColumns; col++)
            {
                frames[row, col] = single;
            }
        }

        idle = single;
    }

    private static void LoadActionSheet(string relativePath, string embeddedName, Bitmap?[,] frames)
    {
        using var sheet = TryLoadNamedPng(relativePath, embeddedName, WalkSheetWidth, WalkSheetHeight);
        if (sheet is null)
        {
            return;
        }

        for (var row = 0; row < WalkClock.SheetRows; row++)
        {
            for (var col = 0; col < WalkClock.SheetColumns; col++)
            {
                frames[row, col] = CropCell(sheet, col * NativeSize, row * NativeSize);
            }
        }
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

    /// <summary>Brown cloak stand-in if <c>npc-walk.png</c> is missing.</summary>
    private static Bitmap CreateNpcFallback()
    {
        var bmp = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.Transparent);
        using var cloak = new SolidBrush(Color.FromArgb(127, 89, 63));
        using var cloakDark = new SolidBrush(Color.FromArgb(86, 60, 43));
        using var skin = new SolidBrush(Color.FromArgb(255, 229, 229));
        using var outline = new SolidBrush(Color.FromArgb(26, 18, 14));
        g.FillRectangle(outline, 10, 6, 12, 10);
        g.FillRectangle(skin, 11, 7, 10, 8);
        g.FillRectangle(outline, 8, 15, 16, 15);
        g.FillRectangle(cloak, 9, 16, 14, 13);
        g.FillRectangle(cloakDark, 9, 24, 5, 5);
        g.FillRectangle(cloakDark, 18, 24, 5, 5);
        return bmp;
    }

    /// <summary>Green slime stand-in if <c>monster-walk.png</c> is missing.</summary>
    private static Bitmap CreateMonsterFallback()
    {
        var bmp = new Bitmap(NativeSize, NativeSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.Transparent);
        using var body = new SolidBrush(Color.FromArgb(46, 168, 72));
        using var dark = new SolidBrush(Color.FromArgb(28, 110, 48));
        using var hi = new SolidBrush(Color.FromArgb(96, 210, 110));
        using var eye = new SolidBrush(Color.FromArgb(26, 18, 14));
        g.FillEllipse(dark, 5, 12, 22, 18);
        g.FillEllipse(body, 6, 13, 20, 16);
        g.FillEllipse(hi, 10, 16, 6, 4);
        g.FillRectangle(eye, 11, 18, 3, 3);
        g.FillRectangle(eye, 18, 18, 3, 3);
        return bmp;
    }
}
