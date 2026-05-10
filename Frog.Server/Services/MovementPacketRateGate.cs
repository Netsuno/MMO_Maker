using System.Collections.Generic;

namespace Frog.Server.Services;

/// <summary>Limite le débit des paquets mouvement / sync position par session (anti-spam minimal).</summary>
public sealed class MovementPacketRateGate
{
    /// <summary>Fenêtre glissante d’1 s ; au-delà les paquets sont refusés côté dispatcher.</summary>
    public const int MaxPacketsPerRollingSecond = 50;

    private readonly Queue<DateTime> _timestamps = new();

    public bool TryConsume(DateTime utcNow)
    {
        while (_timestamps.Count > 0 && (utcNow - _timestamps.Peek()).TotalSeconds >= 1.0)
        {
            _timestamps.Dequeue();
        }

        if (_timestamps.Count >= MaxPacketsPerRollingSecond)
        {
            return false;
        }

        _timestamps.Enqueue(utcNow);
        return true;
    }
}
