using Frog.Application.Identity;
using Frog.Core.Security;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresAuthRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresAuthRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task AccountAndSession_RoundTrip_RevokeAndDuplicate()
    {
        using var gate = CreateGate();
        var accounts = new PostgresAccountRepository(gate);
        var sessions = new PostgresAuthSessionRepository(gate);

        var created = await accounts.TryCreateAsync("pg-auth-user", "password12345");
        Assert.Equal(AccountCreateStatus.Created, created.Status);
        Assert.NotNull(created.AccountId);

        var found = await accounts.FindByUsernameAsync("pg-auth-user");
        Assert.NotNull(found);
        Assert.Equal(created.AccountId, found!.Id);
        Assert.True(PasswordHasher.VerifyPassword("password12345", found.PasswordHash));

        var duplicate = await accounts.TryCreateAsync("pg-auth-user", "password12345");
        Assert.Equal(AccountCreateStatus.DuplicateUsername, duplicate.Status);

        var issued = await sessions.IssueAsync(created.AccountId!.Value, TimeSpan.FromHours(2));
        Assert.Equal(AuthSessionIssueStatus.Issued, issued.Status);
        Assert.False(string.IsNullOrEmpty(issued.Token));

        var valid = await sessions.ValidateTokenAsync(issued.Token!);
        Assert.Equal(AuthSessionValidationStatus.Valid, valid.Status);

        Assert.True(await sessions.RevokeAsync(issued.Session!.Id));
        var revoked = await sessions.ValidateTokenAsync(issued.Token!);
        Assert.Equal(AuthSessionValidationStatus.Revoked, revoked.Status);

        var byId = await accounts.FindByIdAsync(created.AccountId.Value);
        Assert.NotNull(byId);
        Assert.Equal("pg-auth-user", byId!.Username);
    }

    private FrogDbContextGate CreateGate()
        => new(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
}
