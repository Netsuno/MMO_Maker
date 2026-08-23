namespace Frog.Application.Playtest;

/// <summary>
/// Jeton playtest à usage unique avec sémantique claim/commit : réservé pendant la tentative de login,
/// consommé uniquement après session créée et login réussi ; libéré si la création échoue.
/// </summary>
public sealed class PlaytestAuthTokenGate
{
    private enum State
    {
        Available,
        Claimed,
        Consumed,
    }

    private readonly object _gate = new();
    private readonly string? _expectedToken;
    private State _state = State.Available;

    public PlaytestAuthTokenGate(string? initialToken)
    {
        _expectedToken = string.IsNullOrEmpty(initialToken) ? null : initialToken;
        _state = _expectedToken is null ? State.Consumed : State.Available;
    }

    /// <summary>Réserve le jeton pour une tentative d’auth playtest (sans le consommer).</summary>
    public bool TryClaim(string? presented)
    {
        lock (_gate)
        {
            if (_state != State.Available || _expectedToken is null || string.IsNullOrEmpty(presented))
            {
                return false;
            }

            if (!PlaytestAuthToken.FixedTimeEquals(_expectedToken, presented))
            {
                return false;
            }

            _state = State.Claimed;
            return true;
        }
    }

    /// <summary>Consomme définitivement après login playtest réussi.</summary>
    public void CommitClaim()
    {
        lock (_gate)
        {
            if (_state == State.Claimed)
            {
                _state = State.Consumed;
            }
        }
    }

    /// <summary>Libère la réservation si création de session / login a échoué.</summary>
    public void ReleaseClaim()
    {
        lock (_gate)
        {
            if (_state == State.Claimed)
            {
                _state = State.Available;
            }
        }
    }

    /// <summary>True s’il reste un jeton non consommé (tests).</summary>
    public bool HasRemainingToken
    {
        get
        {
            lock (_gate)
            {
                return _state == State.Available;
            }
        }
    }

    /// <summary>True si une réservation est en cours (tests).</summary>
    public bool IsClaimed
    {
        get
        {
            lock (_gate)
            {
                return _state == State.Claimed;
            }
        }
    }
}
