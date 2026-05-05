using System;
using Frog.Server.Database;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Applique le schéma MariaDB réel si la variable d'environnement est définie.
/// Aucune chaîne de connexion dans le dépôt : définir <c>MARIADB_TEST_CONNECTION_STRING</c> avant <c>dotnet test</c>.
/// </summary>
public sealed class MariaDbSchemaIntegrationTests
{
    private static string? TestConnectionString =>
        Environment.GetEnvironmentVariable("MARIADB_TEST_CONNECTION_STRING");

    [Fact]
    [Trait("Category", "MariaDb")]
    public void MariaDbSchemaBootstrap_apply_is_idempotent_when_connection_configured()
    {
        var cs = TestConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            return;
        }

        MariaDbSchemaBootstrap.Apply(cs);
        MariaDbSchemaBootstrap.Apply(cs);
    }
}
