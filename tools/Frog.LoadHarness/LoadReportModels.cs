using Frog.Server.Observability;

namespace Frog.LoadHarness;

public sealed class LoadHarnessReport
{
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset EndedUtc { get; init; }
    public long ElapsedMs { get; init; }
    public string Scenario { get; init; } = "";
    public int RequestedSessions { get; init; }
    public LoadHostInfo Host { get; init; } = new();
    public LoadMachineInfo Machine { get; init; } = new();
    public LoadClientCounters Client { get; init; } = new();
    public ServerOpsSnapshot? ServerOps { get; init; }
    public LoadCampaignInfo? Campaign { get; init; }
}

public sealed class LoadHostInfo
{
    public string Mode { get; init; } = "";
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string TlsMode { get; init; } = "Off";
    public string? TlsTargetHost { get; init; }
}

public sealed class LoadMachineInfo
{
    public string Os { get; init; } = "";
    public string Framework { get; init; } = "";
    public int ProcessorCount { get; init; }
    public double ProcessCpuPercentEstimate { get; init; }
    public long ProcessWorkingSetBytes { get; init; }
    public string HostName { get; init; } = "";
}

public sealed class LoadClientCounters
{
    public int TcpConnectOk;
    public int TcpConnectFail;
    public int HelloOk;
    public int HelloFail;
    public int RegisterOk;
    public int RegisterFail;
    public int LoginOk;
    public int LoginFail;
    public int CharacterCreateOk;
    public int CharacterCreateFail;
    public int CharacterSelectOk;
    public int CharacterSelectFail;
    public int AuthenticateException;
    public int ChatSent;
    public int ChatSendFail;
    public int ChatMessageRecv;
    public int ChatRateLimited;
    public int MoveSent;
    public int MoveSendFail;
    public int PositionUpdateRecv;
    public int MoveRateLimited;
    public long MoveBurstElapsedMs;
    public int OtherErrors;
    public int OversizeDropped;
    public int OversizeStillConnected;
    public int OversizeProbeFail;
    public int LoginProbeRejected;
    public int LoginProbeFail;
    public int HeartbeatSent;
    public int HeartbeatAckRecv;
    public int HeartbeatFail;
    public int InteractSent;
    public int InteractResultRecv;
    public int InteractFail;
    public int MeleeSent;
    public int MeleeResultRecv;
    public int MeleeFail;
}

public sealed class LoadCampaignInfo
{
    public const long MandateHoldMilliseconds = 3_600_000;

    public long MandateHoldMs { get; init; } = MandateHoldMilliseconds;
    public long ActualHoldMs { get; init; }
    public bool MandateDurationMet { get; init; }
    public string MandateGap { get; init; } = "";
    public double ActionsPerSecond { get; init; }
    public LoadLatencyStats HeartbeatRtt { get; init; } = new();
    public LoadLatencyStats MoveRtt { get; init; } = new();
    public LoadLatencyStats InteractRtt { get; init; } = new();
    public IReadOnlyList<LoadResourceSample> ResourceSamples { get; init; } = [];
}

public sealed class LoadLatencyStats
{
    public int Count { get; init; }
    public double MeanMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double MaxMs { get; init; }
}

public sealed class LoadResourceSample
{
    public long ElapsedMs { get; init; }
    public double ProcessCpuPercentEstimate { get; init; }
    public long WorkingSetBytes { get; init; }
}
