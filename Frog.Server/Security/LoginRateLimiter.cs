using System.Collections.Concurrent;

namespace Frog.Server.Security;

/// <summary>Limite les tentatives login/register par clé (IP ou IP+username).</summary>
public sealed class LoginRateLimiter
{
    private readonly int _maxFailures;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, WindowState> _windows = new();

    public LoginRateLimiter(int maxFailures = 8, TimeSpan? window = null)
    {
        _maxFailures = maxFailures;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool TryAllow(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var now = DateTimeOffset.UtcNow;
        var state = _windows.GetOrAdd(key, _ => new WindowState());
        lock (state)
        {
            ResetIfExpired(state, now);
            return state.FailureCount < _maxFailures;
        }
    }

    public void RegisterFailure(string key)
    {
        var now = DateTimeOffset.UtcNow;
        var state = _windows.GetOrAdd(key, _ => new WindowState());
        lock (state)
        {
            ResetIfExpired(state, now);
            state.FailureCount++;
            state.WindowStartUtc = now;
        }
    }

    public void RegisterSuccess(string key)
    {
        _windows.TryRemove(key, out _);
    }

    private void ResetIfExpired(WindowState state, DateTimeOffset now)
    {
        if (now - state.WindowStartUtc > _window)
        {
            state.FailureCount = 0;
            state.WindowStartUtc = now;
        }
    }

    private sealed class WindowState
    {
        public int FailureCount;
        public DateTimeOffset WindowStartUtc = DateTimeOffset.UtcNow;
    }
}
