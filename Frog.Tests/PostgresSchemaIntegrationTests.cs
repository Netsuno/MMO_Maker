using System;
using Frog.Server.Database;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Applique le schéma PostgreSQL réel si la variable d'environnement est définie (CI ou machine locale).
/// Ne contient aucune chaîne de connexion : exportez <c>POSTGRES_TEST_CONNECTION_STRING</c> avant <c>dotnet test</c>.
/// </summary>
public sealed class PostgresSchemaIntegrationTests
{
    private static string? TestConnectionString =>
        Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION_STRING");

    [Fact]
    [Trait("Category", "Postgres")]
    public void PostgresSchemaBootstrap_apply_is_idempotent_when_connection_configured()
    {
        var cs = TestConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            // Pas de serveur configuré dans cet environnement — pas d'échec.
            return;
        }

        PostgresSchemaBootstrap.Apply(cs);
        PostgresSchemaBootstrap.Apply(cs);
    }
}
