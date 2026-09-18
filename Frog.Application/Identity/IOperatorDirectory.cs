namespace Frog.Application.Identity;

/// <summary>
/// Source de vérité des privilèges opérateur (P9-2). Pas un drapeau sur
/// <c>auth.accounts</c> : une ligne <c>auth.operators</c> (compte 1:1) accordée hors TCP.
/// </summary>
public interface IOperatorDirectory
{
    Task<bool> IsOperatorAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<OperatorGrantResult> GrantAsync(
        Guid accountId,
        string grantedBy,
        string? note = null,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid accountId, CancellationToken cancellationToken = default);
}

public enum OperatorGrantStatus
{
    Granted,
    AccountNotFound,
    InvalidInput,
}

public sealed record OperatorGrantResult(OperatorGrantStatus Status);
