namespace Frog.Application.Identity;

public enum AccountCreateStatus
{
    Created,
    DuplicateUsername,
    InvalidInput,
    RateLimited,
}

public sealed record AccountCreateResult(AccountCreateStatus Status, Guid? AccountId = null);
