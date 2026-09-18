using System;
using System.IO;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10PostgresRolesTests
{
    [Fact]
    public void Scripts_DefineThreeRolesPlusOpsReco_RuntimeIsNotSuperuser()
    {
        var root = RepoRoot();
        var roles = File.ReadAllText(Path.Combine(root, "scripts", "postgres-roles.sql"));
        var grants = File.ReadAllText(Path.Combine(root, "scripts", "postgres-role-grants.sql"));
        var apply = File.ReadAllText(Path.Combine(root, "scripts", "postgres-apply-roles.sh"));

        Assert.Contains("frog_runtime", roles, System.StringComparison.Ordinal);
        Assert.Contains("frog_migrate", roles, System.StringComparison.Ordinal);
        Assert.Contains("frog_publish", roles, System.StringComparison.Ordinal);
        Assert.Contains("frog_ops", roles, System.StringComparison.Ordinal);
        Assert.Contains("NOSUPERUSER", roles, System.StringComparison.Ordinal);
        Assert.Contains("NOCREATEDB", roles, System.StringComparison.Ordinal);
        Assert.Contains("NOCREATEROLE", roles, System.StringComparison.Ordinal);

        Assert.Contains("SELECT, INSERT, UPDATE, DELETE", grants, System.StringComparison.Ordinal);
        Assert.Contains("REVOKE CREATE ON SCHEMA", grants, System.StringComparison.Ordinal);
        Assert.DoesNotContain("SUPERUSER", grants, System.StringComparison.Ordinal);

        Assert.Contains("postgres-role-grants.sql", apply, System.StringComparison.Ordinal);
        Assert.Contains("FROG_PG_RUNTIME_PASSWORD", apply, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeDemo_IsInstanceSuperuser_NotHostedRuntime()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "docker-compose.yml"));
        Assert.Contains("POSTGRES_USER: frog", text, System.StringComparison.Ordinal);
        Assert.Contains("PAS le profil hébergé", text, System.StringComparison.Ordinal);
        Assert.Contains("frog_runtime", text, System.StringComparison.Ordinal);
        Assert.Contains("démo locale", text, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalExample_UsesFrogRuntimeUsername()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Server", "appsettings.Local.json.example"));
        Assert.Contains("Username=frog_runtime", json, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Username=frog;", json, System.StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new System.InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
