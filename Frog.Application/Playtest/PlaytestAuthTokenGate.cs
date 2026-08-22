namespace Frog.Application.Playtest;

/// <summary>
/// Jeton playtest consommable une seule fois (atomic). Après succès, toute réutilisation échoue.
/// </summary>
public sealed class PlaytestAuthTokenGate
{
    private readonly object _gate = new();
    private string? _remaining;

    public PlaytestAuthTokenGate(string? initialToken)
    {
        _remaining = string.IsNullOrEmpty(initialToken) ? null : initialToken;
    }

    /// <summary>True si le jeton présenté correspond et est consommé atomiquement.</summary>
    public bool TryConsume(string? presented)
    {
        lock (_gate)
        {
            if (_remaining is null || string.IsNullOrEmpty(presented))
            {
                return false;
            }

            if (!PlaytestAuthToken.FixedTimeEquals(_remaining, presented))
            {
                return false;
            }

            _remaining = null;
            return true;
        }
    }

    /// <summary>True s’il reste un jeton non consommé (tests).</summary>
    public bool HasRemainingToken
    {
        get
        {
            lock (_gate)
            {
                return _remaining is not null;
            }
        }
    }
}
