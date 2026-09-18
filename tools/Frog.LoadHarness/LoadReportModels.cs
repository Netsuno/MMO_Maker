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
}
