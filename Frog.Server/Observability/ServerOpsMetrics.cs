using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frog.Server.Observability;

/// <summary>
/// Process-local ops counters for a small hosted world (P9-5).
/// Not a metrics HTTP endpoint — snapshot via structured log and optional JSON file.
/// </summary>
public sealed class ServerOpsMetrics
{
    private long _connectionsAccepted;
    private long _connectionsRejected;
    private long _rateLimitHits;
    private long _rateLimitHitsLogin;
    private long _rateLimitHitsReconnect;
    private long _rateLimitHitsChat;
    private long _rateLimitHitsMovement;
    private long _postgresErrors;
    private readonly ConcurrentDictionary<string, long> _rejectReasons = new(StringComparer.Ordinal);

    public void RecordConnectionAccepted() => Interlocked.Increment(ref _connectionsAccepted);

    public void RecordConnectionRejected(string reason)
    {
        Interlocked.Increment(ref _connectionsRejected);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _rejectReasons.AddOrUpdate(reason, 1, static (_, n) => n + 1);
        }
    }

    public void RecordRateLimitHit(string kind)
    {
        Interlocked.Increment(ref _rateLimitHits);
        switch (kind)
        {
            case "login":
                Interlocked.Increment(ref _rateLimitHitsLogin);
                break;
            case "reconnect":
                Interlocked.Increment(ref _rateLimitHitsReconnect);
                break;
            case "chat":
                Interlocked.Increment(ref _rateLimitHitsChat);
                break;
            case "movement":
                Interlocked.Increment(ref _rateLimitHitsMovement);
                break;
        }
    }

    public void RecordPostgresError() => Interlocked.Increment(ref _postgresErrors);

    /// <summary>
    /// True when the exception (or an inner) looks like Npgsql / PostgreSQL.
    /// Frog.Server does not compile against Npgsql — match on type name.
    /// </summary>
    public static bool IsPostgresError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().FullName ?? e.GetType().Name;
            if (name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PostgresException", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool RecordIfPostgresError(Exception ex)
    {
        if (!IsPostgresError(ex))
        {
            return false;
        }

        RecordPostgresError();
        return true;
    }

    public ServerOpsSnapshot Snapshot(int activeSessions = 0)
        => new(
            DateTimeOffset.UtcNow,
            Volatile.Read(ref _connectionsAccepted),
            Volatile.Read(ref _connectionsRejected),
            Volatile.Read(ref _rateLimitHits),
            Volatile.Read(ref _rateLimitHitsLogin),
            Volatile.Read(ref _rateLimitHitsReconnect),
            Volatile.Read(ref _rateLimitHitsChat),
            Volatile.Read(ref _rateLimitHitsMovement),
            Volatile.Read(ref _postgresErrors),
            activeSessions,
            _rejectReasons.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal));

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record ServerOpsSnapshot(
    DateTimeOffset Utc,
    long ConnectionsAccepted,
    long ConnectionsRejected,
    long RateLimitHits,
    long RateLimitHitsLogin,
    long RateLimitHitsReconnect,
    long RateLimitHitsChat,
    long RateLimitHitsMovement,
    long PostgresErrors,
    int ActiveSessions,
    IReadOnlyDictionary<string, long> RejectReasons);
