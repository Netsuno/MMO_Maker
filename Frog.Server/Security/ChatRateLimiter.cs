using System.Collections.Concurrent;
using Frog.Core.Gameplay;

namespace Frog.Server.Security;

/// <summary>Limite les messages chat par session (fenêtre glissante).</summary>
public sealed class ChatRateLimiter
{
    private readonly int _maxMessages;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<Guid, WindowState> _windows = new();

    public ChatRateLimiter(
        int maxMessages = GameplayLimits.MaxChatMessagesPerWindow,
        TimeSpan? window = null)
    {
        _maxMessages = maxMessages;
        _window = window ?? TimeSpan.FromSeconds(GameplayLimits.ChatRateWindowSeconds);
    }

    public bool TryAllow(Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var state = _windows.GetOrAdd(sessionId, static _ => new WindowState());
        lock (state)
        {
            Prune(state, now);
            if (state.Timestamps.Count >= _maxMessages)
            {
                return false;
            }

            state.Timestamps.Add(now);
            return true;
        }
    }

    public void Reset(Guid sessionId) => _windows.TryRemove(sessionId, out _);

    private void Prune(WindowState state, DateTimeOffset now)
    {
        var cutoff = now - _window;
        state.Timestamps.RemoveAll(t => t < cutoff);
    }

    private sealed class WindowState
    {
        public List<DateTimeOffset> Timestamps { get; } = new();
    }
}
