namespace Frog.Application.Playtest;

/// <summary>Processus démarré par l’éditeur pour un playtest (serveur ou client).</summary>
public sealed class PlaytestProcessHandle
{
    public required int ProcessId { get; init; }
    public required string Role { get; init; }
    public required string ExecutablePath { get; init; }
}

public sealed class PlaytestServerStartRequest
{
    public required PlaytestLaunchPlan Plan { get; init; }
    public required string ExecutablePath { get; init; }
    public required int Port { get; init; }
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class PlaytestClientStartRequest
{
    public required PlaytestLaunchPlan Plan { get; init; }
    public required string ExecutablePath { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
}

/// <summary>Port d’orchestration processus (implémenté côté éditeur ; mockable en tests).</summary>
public interface IPlaytestProcessLauncher
{
    Task<PlaytestProcessHandle> StartServerAsync(
        PlaytestServerStartRequest request,
        CancellationToken cancellationToken = default);

    Task<PlaytestProcessHandle> StartClientAsync(
        PlaytestClientStartRequest request,
        CancellationToken cancellationToken = default);

    Task StopAsync(PlaytestProcessHandle handle, CancellationToken cancellationToken = default);

    bool IsRunning(PlaytestProcessHandle handle);
}

/// <summary>État d’une session playtest orchestrée.</summary>
public sealed class PlaytestSessionState
{
    public required Guid CorrelationId { get; init; }
    public PlaytestLaunchPlan? Plan { get; init; }
    public PlaytestProcessHandle? Server { get; set; }
    public PlaytestProcessHandle? Client { get; set; }
    public bool IsActive { get; set; }
    public readonly List<string> LogLines = new();
}

public interface IPlaytestOrchestrator
{
    PlaytestSessionState? ActiveSession { get; }

    Task<PlaytestPreparationResult> StartAsync(
        Maps.MapWorkspaceSession workspace,
        PlaytestPrepareRequest prepareRequest,
        string serverExe,
        string clientExe,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestre préparation + lancement serveur/client + cleanup. Aucun accès DB direct.
/// </summary>
public sealed class PlaytestOrchestrator : IPlaytestOrchestrator
{
    private readonly IPlaytestMapPreparer _preparer;
    private readonly IPlaytestProcessLauncher _launcher;
    private readonly object _gate = new();
    private PlaytestSessionState? _active;

    public PlaytestOrchestrator(IPlaytestMapPreparer preparer, IPlaytestProcessLauncher launcher)
    {
        _preparer = preparer ?? throw new ArgumentNullException(nameof(preparer));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public PlaytestSessionState? ActiveSession
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    public async Task<PlaytestPreparationResult> StartAsync(
        Maps.MapWorkspaceSession workspace,
        PlaytestPrepareRequest prepareRequest,
        string serverExe,
        string clientExe,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(prepareRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverExe);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientExe);

        lock (_gate)
        {
            if (_active is { IsActive: true })
            {
                return new PlaytestPreparationResult.Failed(
                    "Un playtest est déjà en cours. Arrêtez-le avant d’en lancer un autre.",
                    PlaytestFailureKind.LaunchFailure);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var prepared = await _preparer.PrepareAsync(workspace, prepareRequest, cancellationToken)
            .ConfigureAwait(false);
        if (prepared is not PlaytestPreparationResult.Success success)
        {
            return prepared;
        }

        var plan = success.Plan;
        var state = new PlaytestSessionState
        {
            CorrelationId = plan.CorrelationId,
            Plan = plan,
            IsActive = true,
        };
        state.LogLines.Add($"[{plan.CorrelationId:N}] Playtest préparé MapId={plan.PrimaryCanonicalMapId} rev={plan.PrimaryPublishedRevision}");

        lock (_gate)
        {
            _active = state;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var port = plan.Port > 0 ? plan.Port : throw new InvalidOperationException("Port playtest non assigné.");
            state.LogLines.Add($"[{plan.CorrelationId:N}] Démarrage serveur port={port}");
            state.Server = await _launcher.StartServerAsync(
                    new PlaytestServerStartRequest
                    {
                        Plan = plan,
                        ExecutablePath = serverExe,
                        Port = port,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            state.LogLines.Add($"[{plan.CorrelationId:N}] Serveur PID={state.Server.ProcessId}");

            cancellationToken.ThrowIfCancellationRequested();
            state.LogLines.Add($"[{plan.CorrelationId:N}] Démarrage client {plan.Host}:{port}");
            state.Client = await _launcher.StartClientAsync(
                    new PlaytestClientStartRequest
                    {
                        Plan = plan,
                        ExecutablePath = clientExe,
                        Host = plan.Host,
                        Port = port,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            state.LogLines.Add($"[{plan.CorrelationId:N}] Client PID={state.Client.ProcessId}");
            return success;
        }
        catch (OperationCanceledException)
        {
            await CleanupUnsafeAsync(state, CancellationToken.None).ConfigureAwait(false);
            ClearActive(state);
            return new PlaytestPreparationResult.Failed(
                "Playtest annulé.",
                PlaytestFailureKind.Cancellation);
        }
        catch (TimeoutException ex)
        {
            await CleanupUnsafeAsync(state, CancellationToken.None).ConfigureAwait(false);
            ClearActive(state);
            return new PlaytestPreparationResult.Failed(
                "Délai dépassé au lancement : " + ex.Message,
                PlaytestFailureKind.Timeout);
        }
        catch (Exception ex)
        {
            await CleanupUnsafeAsync(state, CancellationToken.None).ConfigureAwait(false);
            ClearActive(state);
            return new PlaytestPreparationResult.Failed(
                "Échec de lancement playtest : " + ex.Message,
                PlaytestFailureKind.LaunchFailure);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        PlaytestSessionState? state;
        lock (_gate)
        {
            state = _active;
            _active = null;
        }

        if (state is null)
        {
            return;
        }

        await CleanupUnsafeAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private void ClearActive(PlaytestSessionState state)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, state))
            {
                _active = null;
            }
        }
    }

    private async Task CleanupUnsafeAsync(PlaytestSessionState state, CancellationToken cancellationToken)
    {
        state.IsActive = false;
        if (state.Client is { } client)
        {
            try
            {
                state.LogLines.Add($"[{state.CorrelationId:N}] Arrêt client PID={client.ProcessId}");
                await _launcher.StopAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // best-effort
            }
        }

        if (state.Server is { } server)
        {
            try
            {
                state.LogLines.Add($"[{state.CorrelationId:N}] Arrêt serveur PID={server.ProcessId}");
                await _launcher.StopAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // best-effort
            }
        }

        state.Client = null;
        state.Server = null;

        if (state.Plan is { } plan &&
            !string.IsNullOrWhiteSpace(plan.WorkDirectory))
        {
            if (PlaytestWorkspacePaths.TryDeleteOwnedWorkspace(
                    plan.WorkDirectory,
                    plan.CorrelationId,
                    out var cleanupError))
            {
                state.LogLines.Add($"[{plan.CorrelationId:N}] Temp playtest nettoyé: {plan.WorkDirectory}");
            }
            else if (!string.IsNullOrWhiteSpace(cleanupError))
            {
                state.LogLines.Add(
                    $"[{plan.CorrelationId:N}] Nettoyage temp refusé/échoué: {PlaytestLogSanitizer.Sanitize(cleanupError)}");
            }
        }
    }
}
