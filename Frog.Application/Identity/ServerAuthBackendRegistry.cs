using Microsoft.Extensions.DependencyInjection;

namespace Frog.Application.Identity;

/// <summary>Branche l’auth PostgreSQL côté serveur sans référence compile-time vers Persistence.</summary>
public interface IServerAuthBackend
{
    void Register(IServiceCollection services, string connectionString);
}

public static class ServerAuthBackendRegistry
{
    private static IServerAuthBackend? _backend;

    public static void SetBackend(IServerAuthBackend backend)
        => _backend = backend ?? throw new ArgumentNullException(nameof(backend));

    public static IServerAuthBackend? Backend => _backend;
}
