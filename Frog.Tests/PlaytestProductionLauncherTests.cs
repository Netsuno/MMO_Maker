using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Server.Config;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Production launcher + orchestrator path (not a hand-rolled ProcessStartInfo).
/// Server = real Frog.Server; client readiness exercised via a production-equivalent
/// headless client process that emits FROG_PLAYTEST_READY after auth+map (same marker
/// as Frog.Client). Full Frog.Client GUI path is covered on Windows smoke.
/// </summary>
public sealed class PlaytestProductionLauncherTests
{
    [Fact]
    public async Task OwnedLauncher_Orchestrator_ServerClientReady_Stop_NoOrphan_SafeCleanup()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll), $"missing {serverDll}");

        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(60) };
        var orch = new PlaytestOrchestrator(preparer, launcher);

        var port = GetFreePort();
        var correlation = Guid.NewGuid();

        // External sentinel must survive cleanup.
        var sentinelDir = Path.Combine(Path.GetTempPath(), "frog-sentinel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sentinelDir);
        var sentinelFile = Path.Combine(sentinelDir, "do-not-delete.txt");
        await File.WriteAllTextAsync(sentinelFile, "sentinel");

        var headlessClient = WriteHeadlessPlaytestClientScript();

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = correlation,
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 1,
                SpawnTileY = 1,
                RequireDurablePersistence = false,
                PublishCurrentBeforeLaunch = true,
            },
            serverExe: serverDll,
            clientExe: headlessClient);

        var success = Assert.IsType<PlaytestPreparationResult.Success>(result);
        Assert.True(orch.ActiveSession!.IsActive);
        Assert.NotNull(orch.ActiveSession.Server);
        Assert.NotNull(orch.ActiveSession.Client);
        var serverPid = orch.ActiveSession.Server!.ProcessId;
        var clientPid = orch.ActiveSession.Client!.ProcessId;
        Assert.True(serverPid > 0);
        Assert.True(clientPid > 0);
        Assert.True(launcher.IsRunning(orch.ActiveSession.Server));
        Assert.True(launcher.IsRunning(orch.ActiveSession.Client));

        var logs = launcher.DrainLogsSnapshot();
        Assert.Contains(logs, l => l.Contains("client-ready-observed", StringComparison.OrdinalIgnoreCase)
                                   || l.Contains(PlaytestAuthToken.ReadyStdoutPrefix, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, l => l.Contains(correlation.ToString("N"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, l => l.Contains(success.Plan.AuthToken, StringComparison.Ordinal));
        Assert.All(logs, l => Assert.DoesNotContain("REDACTED_MUST_NOT_LEAK", l, StringComparison.Ordinal));

        // Probe child envs were sanitized (server+client started after Sanitize).
        var leaks = await PlaytestChildEnvironment.ProbeForbiddenKeysInChildAsync("server");
        Assert.Empty(leaks);

        var workDir = success.Plan.WorkDirectory;
        Assert.True(Directory.Exists(workDir));
        Assert.True(PlaytestWorkspacePaths.TryValidateOwnedWorkspace(workDir, correlation, out _));

        await orch.StopAsync();
        Assert.Null(orch.ActiveSession);
        Assert.False(launcher.HasOwnedProcesses);
        Assert.False(await IsPortOpenAsync(port));
        Assert.False(Directory.Exists(workDir), "owned workspace must be deleted by production cleanup");
        Assert.True(File.Exists(sentinelFile), "external sentinel must not be deleted");

        try
        {
            Directory.Delete(sentinelDir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task OwnedLauncher_ClientEarlyExit_FailsWithActionableLogs()
    {
        var serverDll = ResolveServerDll();
        Assert.True(File.Exists(serverDll));
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var workspace = new MapWorkspaceSession(repo);
        await workspace.InitializeAsync();
        var preparer = new PlaytestMapPreparer(repo);
        var launcher = new PlaytestOwnedProcessLauncher { ClientReadyTimeout = TimeSpan.FromSeconds(15) };
        var orch = new PlaytestOrchestrator(preparer, launcher);
        var port = GetFreePort();

        // Client that exits immediately without READY.
        var badClient = WriteExitingClientScript();

        var result = await orch.StartAsync(
            workspace,
            new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = 0,
                SpawnTileY = 0,
                RequireDurablePersistence = false,
            },
            serverExe: serverDll,
            clientExe: badClient);

        var failed = Assert.IsType<PlaytestPreparationResult.Failed>(result);
        Assert.True(
            failed.Kind is PlaytestFailureKind.LaunchFailure or PlaytestFailureKind.Timeout,
            failed.Kind + ": " + failed.Error);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.Null(orch.ActiveSession);
        Assert.False(launcher.HasOwnedProcesses);
        Assert.False(await IsPortOpenAsync(port));
    }

    [Fact]
    public void WorkspaceCleanup_RejectsExternalSentinelDirectory()
    {
        var sentinel = Path.Combine(Path.GetTempPath(), "frog-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sentinel);
        File.WriteAllText(Path.Combine(sentinel, "keep.txt"), "x");
        var corr = Guid.NewGuid();
        Assert.False(PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(sentinel, corr, out var err));
        Assert.Contains("hors racine", err, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(sentinel));
        Directory.Delete(sentinel, recursive: true);
    }

    [Fact]
    public void LogSanitizer_RemovesFullSecretValues()
    {
        const string secret = "Host=super-secret-db;Password=hunter2";
        var raw = "FROG_POSTGRES_CONNECTION_STRING=" + secret + " more";
        var cleaned = PlaytestLogSanitizer.Sanitize(raw, [secret]);
        Assert.DoesNotContain(secret, cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", cleaned, StringComparison.Ordinal);
        Assert.Contains("FROG_POSTGRES_CONNECTION_STRING=***", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("***=", cleaned.Replace("FROG_POSTGRES_CONNECTION_STRING=***", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    private static string WriteHeadlessPlaytestClientScript()
    {
        // Production-equivalent: TCP Hello check, token login, map request, emit READY marker.
        // Implemented as a small C# script compiled? Simpler: shell + dotnet run of a tiny helper.
        // Use prebuilt approach: a bash/python isn't protocol. Use `dotnet exec` of a helper dll we write.
        return BuildAndReturnHeadlessClientDll();
    }

    private static string BuildAndReturnHeadlessClientDll()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-headless-client-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var csproj = Path.Combine(dir, "HeadlessPlaytestClient.csproj");
        var program = Path.Combine(dir, "Program.cs");
        File.WriteAllText(csproj, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="COREPROJ" />
  </ItemGroup>
</Project>
""".Replace("COREPROJ", Path.GetFullPath("/workspace/Frog.Core/Frog.Core.csproj")));
        File.WriteAllText(program, HeadlessClientSource);
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{csproj}\" -c Release -o \"{Path.Combine(dir, "out")}\" --nologo -v q",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(120_000);
        Assert.Equal(0, p.ExitCode);
        var dll = Path.Combine(dir, "out", "HeadlessPlaytestClient.dll");
        Assert.True(File.Exists(dll));
        return dll;
    }

    private static string WriteExitingClientScript()
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(Path.GetTempPath(), "frog-exit-" + Guid.NewGuid().ToString("N") + ".cmd");
            File.WriteAllText(path, "exit /b 7\r\n");
            return path;
        }

        var sh = Path.Combine(Path.GetTempPath(), "frog-exit-" + Guid.NewGuid().ToString("N") + ".sh");
        File.WriteAllText(sh, "#!/bin/sh\nexit 7\n");
        Process.Start("chmod", $"+x \"{sh}\"")?.WaitForExit();
        return sh;
    }

    private const string HeadlessClientSource = """
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;

var host = "127.0.0.1";
var port = 6000;
string? correlation = null;
string? token = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--host" && i + 1 < args.Length) host = args[++i];
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--correlation" && i + 1 < args.Length) correlation = args[++i];
    else if (args[i] == "--playtest-token" && i + 1 < args.Length) token = args[++i];
}
token ??= Environment.GetEnvironmentVariable("FROG_PLAYTEST_AUTH_TOKEN");
if (string.IsNullOrEmpty(token)) { Console.Error.WriteLine("FROG_PLAYTEST_FAIL missing token"); return 2; }

using var tcp = new TcpClient();
await tcp.ConnectAsync(host, port);
await using var stream = tcp.GetStream();
var hello = await ReadFrame(stream);
if (!WireHello.TryParse(hello, out _, out var ver) || ver != FrogWireProtocol.Version)
{
    Console.Error.WriteLine("FROG_PLAYTEST_FAIL bad hello");
    return 3;
}
await WriteFrame(stream, BuildLogin("__frog_playtest__", token));
var login = await ReadUntil(stream, PacketId.LoginResult);
if (login[1] == 0) { Console.Error.WriteLine("FROG_PLAYTEST_FAIL login"); return 4; }
_ = await ReadUntil(stream, PacketId.PositionUpdate);
await WriteFrame(stream, new byte[] { (byte)PacketId.MapRequest });
var map = await ReadUntilAny(stream, PacketId.MapData, PacketId.MapAlreadySynced);
var mapId = map[0] == (byte)PacketId.MapData
    ? BinaryPrimitives.ReadInt32LittleEndian(map.AsSpan(1))
    : BinaryPrimitives.ReadInt32LittleEndian(map.AsSpan(1));
Console.WriteLine($"FROG_PLAYTEST_READY correlation={correlation} map={mapId} x=0 y=0");
await Task.Delay(Timeout.Infinite);
return 0;

static async Task<byte[]> ReadFrame(NetworkStream s)
{
    var lenBuf = new byte[4];
    await ReadExact(s, lenBuf);
    var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
    var payload = new byte[len];
    await ReadExact(s, payload);
    return payload;
}
static async Task WriteFrame(NetworkStream s, byte[] payload)
{
    var frame = new byte[4 + payload.Length];
    BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
    payload.CopyTo(frame, 4);
    await s.WriteAsync(frame);
}
static async Task ReadExact(NetworkStream s, byte[] buf)
{
    var n = 0;
    while (n < buf.Length)
    {
        var r = await s.ReadAsync(buf.AsMemory(n, buf.Length - n));
        if (r == 0) throw new EndOfStreamException();
        n += r;
    }
}
static async Task<byte[]> ReadUntil(NetworkStream s, PacketId id)
{
    while (true)
    {
        var f = await ReadFrame(s);
        if (f[0] == (byte)id) return f;
    }
}
static async Task<byte[]> ReadUntilAny(NetworkStream s, params PacketId[] ids)
{
    while (true)
    {
        var f = await ReadFrame(s);
        if (ids.Any(i => f[0] == (byte)i)) return f;
    }
}
static byte[] BuildLogin(string user, string pass)
{
    var ub = Encoding.UTF8.GetBytes(user);
    var pb = Encoding.UTF8.GetBytes(pass);
    var payload = new byte[1 + 1 + ub.Length + 1 + pb.Length];
    payload[0] = (byte)PacketId.LoginRequest;
    payload[1] = (byte)ub.Length;
    ub.CopyTo(payload, 2);
    payload[2 + ub.Length] = (byte)pb.Length;
    pb.CopyTo(payload, 3 + ub.Length);
    return payload;
}
""";

    private static string ResolveServerDll()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "Frog.Server", "bin", cfg, "net8.0", "Frog.Server.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..", "Frog.Server", "bin", "Release", "net8.0", "Frog.Server.dll"));
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var c = new TcpClient();
            using var cts = new CancellationTokenSource(200);
            await c.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
