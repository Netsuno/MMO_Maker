using System.IO;
using Frog.Core.Animation;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Editor.Assets;

/// <summary>
/// Animations de tileset pour l’aperçu éditeur. Sidecar JSON, pas de bump <c>.fmap</c>.
/// </summary>
internal static class TilesetAnimCatalog
{
    private static readonly TilesetAnimSession Session = new();
    private static readonly long OriginMs = Environment.TickCount64;
    private static System.Windows.Forms.Timer? _timer;
    private static int _signature;
    private static event Action? PreviewFrameChangedCore;

    public static event Action? Changed;

    public static event Action? PreviewFrameChanged
    {
        add
        {
            EnsureTimer();
            PreviewFrameChangedCore += value;
        }
        remove => PreviewFrameChangedCore -= value;
    }

    public static bool PreviewEnabled
    {
        get => Session.PreviewEnabled;
        set
        {
            if (Session.PreviewEnabled == value)
            {
                return;
            }

            Session.PreviewEnabled = value;
            _signature = int.MinValue;
            Changed?.Invoke();
        }
    }

    public static bool HasAnyStrip => Session.HasAnyStrip;

    public static long PreviewElapsedMs => Math.Max(0, Environment.TickCount64 - OriginMs);

    public static void Clear()
    {
        if (!Session.HasAnyStrip)
        {
            return;
        }

        Session.Clear();
        _signature = int.MinValue;
        Changed?.Invoke();
    }

    public static TilesetAnimMarkResult MarkHorizontalSelection(
        int tilesetId,
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int tileSize,
        int sheetWidth,
        int sheetHeight)
    {
        var result = Session.MarkHorizontalSelection(
            tilesetId,
            originX,
            originY,
            widthPx,
            heightPx,
            tileSize,
            sheetWidth,
            sheetHeight);
        if (!result.Ok)
        {
            return result;
        }

        _signature = int.MinValue;
        TryWriteImageSidecar(tilesetId);
        Changed?.Invoke();
        return result;
    }

    public static TilesetAnimMarkResult ClearSelection(
        int tilesetId,
        int originX,
        int originY,
        int widthPx,
        int heightPx,
        int tileSize)
    {
        var result = Session.ClearSelection(tilesetId, originX, originY, widthPx, heightPx, tileSize);
        if (!result.Ok)
        {
            return result;
        }

        _signature = int.MinValue;
        TryWriteImageSidecar(tilesetId);
        Changed?.Invoke();
        return result;
    }

    public static bool TryFrameCount(int tilesetId, int srcX, int srcY, out int frameCount) =>
        Session.TryFrameCount(tilesetId, srcX, srcY, out frameCount);

    public static bool TryGetStrips(int tilesetId, out IReadOnlyList<AnimatedTileStrip> strips)
    {
        if (!Session.TryGet(tilesetId, out var set) || set is null || set.Strips.Count == 0)
        {
            strips = Array.Empty<AnimatedTileStrip>();
            return false;
        }

        strips = set.Strips;
        return true;
    }

    public static bool TryResolveDrawSource(
        int tilesetId,
        int srcX,
        int srcY,
        int tileSize,
        long elapsedMs,
        int sheetWidth,
        int sheetHeight,
        out int drawSrcX,
        out int drawSrcY) =>
        Session.TryResolveDrawSource(
            tilesetId,
            srcX,
            srcY,
            tileSize,
            elapsedMs,
            sheetWidth,
            sheetHeight,
            out drawSrcX,
            out drawSrcY);

    public static void CanonicalizePaintSource(
        int tilesetId,
        int stampX,
        int stampY,
        int stampTilesW,
        int stampTilesH,
        int tileSize,
        ref int srcX,
        ref int srcY) =>
        Session.CanonicalizePaintSource(tilesetId, stampX, stampY, stampTilesW, stampTilesH, tileSize, ref srcX, ref srcY);

    public static void TryAttachImageSidecar(int tilesetId, string imagePath)
    {
        if (tilesetId < 1)
        {
            return;
        }

        var path = TilesetAnimationJson.ImageSidecarPath(imagePath);
        var set = TilesetAnimationJson.TryReadSet(path);
        if (set is null || set.Strips.Count == 0)
        {
            return;
        }

        set.TilesetId = tilesetId;
        Session.SetTileset(set);
        _signature = int.MinValue;
        Changed?.Invoke();
    }

    public static bool TryReplaceFromMapSidecar(string mapFilePath)
    {
        var path = TilesetAnimationJson.MapSidecarPath(mapFilePath);
        var doc = TilesetAnimationJson.TryReadDocument(path);
        if (doc is null)
        {
            return false;
        }

        Session.ReplaceAll(doc);
        _signature = int.MinValue;
        Changed?.Invoke();
        return true;
    }

    public static void WriteMapSidecars(string mapFilePath, string? exportDirectory, IEnumerable<int>? exportedTilesetIds = null)
    {
        var mapPath = TilesetAnimationJson.MapSidecarPath(mapFilePath);
        var doc = Session.HasAnyStrip ? Session.ToDocument() : new TilesetAnimationDocument();
        if (doc.Tilesets.Count == 0)
        {
            TryDelete(mapPath);
        }
        else if (!string.IsNullOrEmpty(mapPath))
        {
            File.WriteAllBytes(mapPath, TilesetAnimationJson.SerializeDocument(doc));
        }

        if (string.IsNullOrEmpty(exportDirectory))
        {
            return;
        }

        var ids = exportedTilesetIds ?? doc.Tilesets.Select(set => set.TilesetId);
        TilesetAnimationJson.SyncImageSidecars(exportDirectory, doc, ids);
    }

    private static void TryWriteImageSidecar(int tilesetId)
    {
        var source = TilesetCache.TryGetSourcePath(tilesetId);
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            return;
        }

        var sidecar = TilesetAnimationJson.ImageSidecarPath(source);
        if (string.IsNullOrEmpty(sidecar))
        {
            return;
        }

        try
        {
            if (!Session.TryGet(tilesetId, out var set) || set is null || set.Strips.Count == 0)
            {
                TryDelete(sidecar);
                return;
            }

            File.WriteAllBytes(sidecar, TilesetAnimationJson.SerializeSet(set));
        }
        catch (IOException)
        {
            // L’aperçu reste en mémoire si le sidecar n’est pas inscriptible.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void EnsureTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return;
        }

        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (_, _) =>
        {
            if (!Session.PreviewEnabled || !Session.HasAnyStrip)
            {
                return;
            }

            var sig = Session.PreviewSignature(PreviewElapsedMs);
            if (sig == _signature)
            {
                return;
            }

            _signature = sig;
            PreviewFrameChangedCore?.Invoke();
        };
        _timer.Start();
    }
}
