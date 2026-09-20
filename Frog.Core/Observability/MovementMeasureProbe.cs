using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Frog.Core.Observability;

/// <summary>
/// Lightweight movement baseline counters. No-op when <see cref="Enabled"/> is false.
/// Does not change pathfinding, prediction, collision, or camera math.
/// </summary>
public sealed class MovementMeasureProbe
{
    public const string LogPrefix = "[measure]";

    private readonly object _gate = new();
    private readonly MovementMeasureBucket _pressToIntent = new("input_press_to_intent_ms");
    private readonly MovementMeasureBucket _intentToVisible = new("render_intent_to_visible_ms");
    private readonly MovementMeasureBucket _sendToLocal = new("net_send_to_local_correction_ms");
    private readonly MovementMeasureBucket _otherInterval = new("net_other_player_update_interval_ms");
    private readonly MovementMeasureBucket _frameDt = new("frame_dt_ms");
    private readonly MovementMeasureBucket _serverApply = new("server_apply_ms");

    private long? _pressTicks;
    private long? _intentTicks;
    private long? _sendTicks;
    private long? _otherLastTicks;
    private long _lastSummaryTicks;
    private int _eventsSinceSummary;
    private int _netLocalCount;
    private int _netOtherCount;
    private int _serverApplyCount;

    public MovementMeasureProbe(bool enabled, Action<string>? log = null)
    {
        Enabled = enabled;
        Log = log;
    }

    public bool Enabled { get; }

    public Action<string>? Log { get; set; }

    public int InputSampleCount
    {
        get
        {
            lock (_gate)
            {
                return _pressToIntent.Count;
            }
        }
    }

    public int RenderSampleCount
    {
        get
        {
            lock (_gate)
            {
                return _intentToVisible.Count;
            }
        }
    }

    public int NetworkLocalSampleCount
    {
        get
        {
            lock (_gate)
            {
                return _sendToLocal.Count;
            }
        }
    }

    public int OtherPlayerSampleCount
    {
        get
        {
            lock (_gate)
            {
                return _otherInterval.Count;
            }
        }
    }

    public int FrameSampleCount
    {
        get
        {
            lock (_gate)
            {
                return _frameDt.Count;
            }
        }
    }

    public int ServerApplySampleCount
    {
        get
        {
            lock (_gate)
            {
                return _serverApply.Count;
            }
        }
    }

    public void NoteKeyPress()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            _pressTicks = Stopwatch.GetTimestamp();
        }
    }

    public void NoteMoveIntent()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            if (_pressTicks is long press)
            {
                var ms = TicksToMs(now - press);
                _pressToIntent.Add(ms);
                EmitImmediate("input press→intent " + FormatMs(ms));
                _pressTicks = null;
            }

            _intentTicks = now;
        }
    }

    public void NoteVisibleUpdate()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (_intentTicks is not long intent)
            {
                return;
            }

            var ms = TicksToMs(Stopwatch.GetTimestamp() - intent);
            _intentToVisible.Add(ms);
            EmitImmediate("render intent→visible " + FormatMs(ms));
            _intentTicks = null;
        }
    }

    public void NoteNetworkSend()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            _sendTicks = Stopwatch.GetTimestamp();
        }
    }

    public void NoteLocalCorrection()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (_sendTicks is not long send)
            {
                return;
            }

            var ms = TicksToMs(Stopwatch.GetTimestamp() - send);
            _sendToLocal.Add(ms);
            _sendTicks = null;
            _netLocalCount++;
            if (_netLocalCount <= 4 || _netLocalCount % 10 == 0)
            {
                EmitImmediate("net send→local " + FormatMs(ms));
            }

            MaybeEmitSummaryLocked();
        }
    }

    public void NoteOtherPlayerUpdate()
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            if (_otherLastTicks is long last)
            {
                var ms = TicksToMs(now - last);
                _otherInterval.Add(ms);
                _netOtherCount++;
                if (_netOtherCount <= 4 || _netOtherCount % 10 == 0)
                {
                    EmitImmediate("net other-player interval " + FormatMs(ms));
                }
            }

            _otherLastTicks = now;
            MaybeEmitSummaryLocked();
        }
    }

    public void NoteFrameTime(double dtMilliseconds)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (dtMilliseconds < 0)
            {
                dtMilliseconds = 0;
            }

            _frameDt.Add(dtMilliseconds);
            MaybeEmitSummaryLocked();
        }
    }

    public void NoteServerApply(double applyMilliseconds)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            if (applyMilliseconds < 0)
            {
                applyMilliseconds = 0;
            }

            _serverApply.Add(applyMilliseconds);
            _serverApplyCount++;
            if (_serverApplyCount <= 8 || _serverApplyCount % 20 == 0)
            {
                EmitImmediate("server apply " + FormatMs(applyMilliseconds));
            }

            MaybeEmitSummaryLocked();
        }
    }

    public string FormatSummary()
    {
        if (!Enabled)
        {
            return LogPrefix + " off";
        }

        lock (_gate)
        {
            return FormatSummaryLocked();
        }
    }

    private void MaybeEmitSummaryLocked()
    {
        _eventsSinceSummary++;
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = _lastSummaryTicks == 0
            ? 2001
            : TicksToMs(now - _lastSummaryTicks);
        if (elapsedMs < 2000 && _eventsSinceSummary < 12)
        {
            return;
        }

        _lastSummaryTicks = now;
        _eventsSinceSummary = 0;
        EmitImmediate(FormatSummaryLocked());
    }

    private string FormatSummaryLocked()
    {
        var sb = new StringBuilder();
        sb.Append(LogPrefix);
        sb.Append(" summary ");
        sb.Append(_pressToIntent.Format());
        sb.Append(" | ");
        sb.Append(_intentToVisible.Format());
        sb.Append(" | ");
        sb.Append(_sendToLocal.Format());
        sb.Append(" | ");
        sb.Append(_otherInterval.Format());
        sb.Append(" | ");
        sb.Append(_frameDt.Format());
        sb.Append(" | ");
        sb.Append(_serverApply.Format());
        if (_frameDt.Count > 0)
        {
            sb.Append(" | fps_note last_dt=");
            sb.Append(FormatMs(_frameDt.Last));
            sb.Append(" vs timer=16ms (predict step is dt-scaled)");
        }

        return sb.ToString();
    }

    private void EmitImmediate(string body)
    {
        var line = body.StartsWith(LogPrefix, StringComparison.Ordinal)
            ? body
            : LogPrefix + " " + body;
        Log?.Invoke(line);
        Debug.WriteLine(line);
    }

    private static double TicksToMs(long deltaTicks)
        => deltaTicks <= 0 ? 0 : deltaTicks * 1000.0 / Stopwatch.Frequency;

    private static string FormatMs(double ms)
        => ms.ToString("0.0", CultureInfo.InvariantCulture) + "ms";

    private sealed class MovementMeasureBucket(string name)
    {
        public int Count { get; private set; }

        public double Min { get; private set; } = double.PositiveInfinity;

        public double Max { get; private set; }

        public double Sum { get; private set; }

        public double Last { get; private set; }

        public double Mean => Count == 0 ? 0 : Sum / Count;

        public void Add(double ms)
        {
            Count++;
            Sum += ms;
            Last = ms;
            if (ms < Min)
            {
                Min = ms;
            }

            if (ms > Max)
            {
                Max = ms;
            }
        }

        public string Format()
        {
            if (Count == 0)
            {
                return name + ": n=0";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: n={1} last={2:0.0} min={3:0.0} mean={4:0.0} max={5:0.0}",
                name,
                Count,
                Last,
                Min,
                Mean,
                Max);
        }
    }
}
