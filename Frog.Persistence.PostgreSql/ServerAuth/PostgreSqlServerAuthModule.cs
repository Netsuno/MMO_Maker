using Frog.Persistence.PostgreSql.ServerAuth;

internal static class PostgreSqlServerAuthModule
{
#pragma warning disable CA2255
    [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => PostgreSqlServerAuthBackendRegistration.Register();
}
