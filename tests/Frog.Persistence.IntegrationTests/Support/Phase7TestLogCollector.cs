using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Frog.Persistence.IntegrationTests.Support;

/// <summary>
/// In-memory logger that lifecycle tests use to fail on unexpected Error/Critical entries
/// (e.g. "Client handler task faulted unexpectedly").
/// </summary>
internal sealed class Phase7TestLogCollector : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly ConcurrentDictionary<string, Phase7TestLogCollector> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, _ => this);

    public void Dispose()
    {
        _loggers.Clear();
    }

    IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

    bool ILogger.IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    void ILogger.Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel < LogLevel.Debug)
        {
            return;
        }

        _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    public IReadOnlyList<LogEntry> GetEntries() => _entries.ToArray();

    public void AssertNoUnexpectedErrors()
    {
        var failures = _entries
            .Where(e => e.Level >= LogLevel.Error)
            .ToArray();

        if (failures.Length == 0)
        {
            return;
        }

        var details = string.Join(
            Environment.NewLine,
            failures.Select(e =>
                e.Exception is null
                    ? $"[{e.Level}] {e.Message}"
                    : $"[{e.Level}] {e.Message}{Environment.NewLine}{e.Exception}"));

        throw new Xunit.Sdk.XunitException(
            $"Unexpected server log entries ({failures.Length}):{Environment.NewLine}{details}");
    }

    internal readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
