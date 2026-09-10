using System.Globalization;
using System.Text.RegularExpressions;
using Frog.Core.Constants;

namespace Frog.Application.Playtest;

/// <summary>
/// Marqueur stdout READY playtest — parse strict (pas de <c>Contains</c> partiel).
/// Format:
/// <c>FROG_PLAYTEST_READY correlation={N} map={id} tileX={tx} tileY={ty} pixelX={px} pixelY={py}</c>
/// </summary>
public static partial class PlaytestReadyMarker
{
    public const string Prefix = PlaytestAuthToken.ReadyStdoutPrefix;

    [GeneratedRegex(
        @"^FROG_PLAYTEST_READY correlation=(?<corr>[0-9a-fA-F]{32}) map=(?<map>-?\d+) tileX=(?<tx>-?\d+) tileY=(?<ty>-?\d+) pixelX=(?<px>-?\d+) pixelY=(?<py>-?\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex MarkerRegex();

    public static string Format(
        Guid correlationId,
        int runtimeMapId,
        int tileX,
        int tileY,
        int pixelX,
        int pixelY)
        => $"{Prefix} correlation={correlationId:N} map={runtimeMapId} tileX={tileX} tileY={tileY} pixelX={pixelX} pixelY={pixelY}";

    public static bool TryParse(string? line, out PlaytestReadyValues values)
    {
        values = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        // Logs launcher may prefix "[correlation] [client:out] …"
        var idx = line.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var candidate = line[idx..].Trim();
        var m = MarkerRegex().Match(candidate);
        if (!m.Success)
        {
            return false;
        }

        if (!Guid.TryParseExact(m.Groups["corr"].Value, "N", out var corr))
        {
            return false;
        }

        if (!int.TryParse(m.Groups["map"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var map)
            || !int.TryParse(m.Groups["tx"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tx)
            || !int.TryParse(m.Groups["ty"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ty)
            || !int.TryParse(m.Groups["px"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px)
            || !int.TryParse(m.Groups["py"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var py))
        {
            return false;
        }

        values = new PlaytestReadyValues(corr, map, tx, ty, px, py);
        return true;
    }

    public static bool TryValidateAgainstPlan(
        string? line,
        Guid expectedCorrelation,
        PlaytestSpawnPoint spawn,
        out PlaytestReadyValues values,
        out string? error)
    {
        values = default;
        error = null;
        if (!TryParse(line, out values))
        {
            error = "READY marker malformed or incomplete.";
            return false;
        }

        if (values.CorrelationId != expectedCorrelation)
        {
            error = "READY correlation mismatch.";
            return false;
        }

        if (values.RuntimeMapId != spawn.RuntimeMapId)
        {
            error = $"READY map mismatch: got {values.RuntimeMapId}, expected {spawn.RuntimeMapId}.";
            return false;
        }

        if (values.TileX != spawn.TileX || values.TileY != spawn.TileY)
        {
            error = $"READY tile mismatch: got ({values.TileX},{values.TileY}), expected ({spawn.TileX},{spawn.TileY}).";
            return false;
        }

        var (expectedPx, expectedPy) = WorldMetrics.TileCenterToPixels(spawn.TileX, spawn.TileY);
        if (values.PixelX != expectedPx || values.PixelY != expectedPy)
        {
            error =
                $"READY pixel mismatch: got ({values.PixelX},{values.PixelY}), expected ({expectedPx},{expectedPy}).";
            return false;
        }

        return true;
    }

    public static (int TileX, int TileY) PixelsToTile(int pixelX, int pixelY)
        => (pixelX / WorldMetrics.DefaultTileSizePixels, pixelY / WorldMetrics.DefaultTileSizePixels);
}

public readonly record struct PlaytestReadyValues(
    Guid CorrelationId,
    int RuntimeMapId,
    int TileX,
    int TileY,
    int PixelX,
    int PixelY);
