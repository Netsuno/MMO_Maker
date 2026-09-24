using Frog.Core.Maps;

namespace Frog.Core.Animation;

/// <summary>
/// Animation de tuile par groupe de <see cref="TileAssetId"/> (frames ordonnées + durée).
/// Remplacement visé des bandes <c>.anim.json</c> ; ce type ne migre pas l’éditeur.
/// L’index de frame réutilise <see cref="AnimatedTileFrames.FrameIndex"/> (y compris le ping-pong à 3 frames).
/// </summary>
public sealed class TileAnimation
{
    public string Name { get; set; } = string.Empty;

    public int FrameDurationMs { get; set; } = AnimatedTileFrames.DefaultFrameDurationMs;

    public List<TileAssetId> Frames { get; } = new();

    public bool Validate(out string? error)
    {
        if (Frames.Count is < AnimatedTileFrames.MinFrameCount or > AnimatedTileFrames.MaxFrameCount)
        {
            error = $"Une animation de tuiles a entre {AnimatedTileFrames.MinFrameCount} et {AnimatedTileFrames.MaxFrameCount} frames.";
            return false;
        }

        if (FrameDurationMs is < AnimatedTileFrames.MinFrameDurationMs or > AnimatedTileFrames.MaxFrameDurationMs)
        {
            error = $"Durée de frame hors plage ({AnimatedTileFrames.MinFrameDurationMs}–{AnimatedTileFrames.MaxFrameDurationMs} ms).";
            return false;
        }

        for (var i = 0; i < Frames.Count; i++)
        {
            if (Frames[i].IsNone)
            {
                error = $"Frame {i} sans TileAssetId.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public TileAssetId FrameAt(long elapsedMs)
    {
        if (!Validate(out var error))
        {
            throw new InvalidOperationException(error);
        }

        var index = AnimatedTileFrames.FrameIndex(Frames.Count, elapsedMs, FrameDurationMs);
        return Frames[index];
    }
}
