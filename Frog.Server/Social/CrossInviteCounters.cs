using System.Collections.Concurrent;
using Frog.Core.Constants;

namespace Frog.Server.Social;

/// <summary>
/// Compteurs d'invitations en mémoire (groupe + échange) et budget anti-spam partagé.
/// </summary>
public sealed class CrossInviteCounters
{
    private readonly ConcurrentDictionary<string, (Func<Guid, int> Outgoing, Func<Guid, int> Incoming)> _providers =
        new();
    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> _inviteRate = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _reinviteCooldown = new();

    public void Register(string name, Func<Guid, int> outgoing, Func<Guid, int> incoming)
        => _providers[name] = (outgoing, incoming);

    public int MemoryOutgoing(Guid characterId)
    {
        var total = 0;
        foreach (var provider in _providers.Values)
        {
            total += provider.Outgoing(characterId);
        }

        return total;
    }

    public int MemoryIncoming(Guid characterId)
    {
        var total = 0;
        foreach (var provider in _providers.Values)
        {
            total += provider.Incoming(characterId);
        }

        return total;
    }

    public bool TryAllowInvite(
        Guid from,
        Guid to,
        string kindKey,
        DateTimeOffset now,
        int ratePerMinute,
        int cooldownSeconds,
        out string error)
    {
        error = string.Empty;
        var key = from.ToString("N") + ":" + to.ToString("N") + ":" + kindKey;
        if (_reinviteCooldown.TryGetValue(key, out var until) && until > now)
        {
            error = "Patientez avant de renvoyer une invitation.";
            return false;
        }

        var q = _inviteRate.GetOrAdd(from, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > TimeSpan.FromMinutes(1))
            {
                q.Dequeue();
            }

            if (q.Count >= ratePerMinute)
            {
                error = "Trop d'invitations.";
                return false;
            }

            q.Enqueue(now);
        }

        _ = cooldownSeconds;
        return true;
    }

    public void MarkCooldown(Guid from, Guid to, string kindKey, DateTimeOffset now, int cooldownSeconds)
    {
        var key = from.ToString("N") + ":" + to.ToString("N") + ":" + kindKey;
        _reinviteCooldown[key] = now.AddSeconds(cooldownSeconds > 0
            ? cooldownSeconds
            : SocialProtocolLimits.ReinviteCooldownSeconds);
    }
}
