using System.Globalization;
using Frog.Core.Observability;

namespace Frog.Server.Observability;

/// <summary>
/// Throttled server-side apply timings. Silent unless <see cref="MovementMeasureOptions"/> is on.
/// Does not change movement apply / warp / broadcast.
/// </summary>
internal static class MovementMeasureServerSink
{
    private static int _applies;

    public static bool IsEnabled => MovementMeasureOptions.IsEnabledFromEnvironment();

    public static bool ShouldLogApply()
    {
        if (!IsEnabled)
        {
            return false;
        }

        var n = Interlocked.Increment(ref _applies);
        return n <= 8 || n % 20 == 0;
    }

    public static string FormatApplyMs(double milliseconds)
        => milliseconds.ToString("0.0", CultureInfo.InvariantCulture);
}
