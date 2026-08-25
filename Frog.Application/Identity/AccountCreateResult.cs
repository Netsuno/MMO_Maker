namespace Frog.Application.Identity;

public enum AccountCreateStatus
{
    Created,
    DuplicateUsername,
    InvalidInput,
}

public sealed record AccountCreateResult(AccountCreateStatus Status, Guid? AccountId = null);
