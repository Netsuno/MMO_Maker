#nullable enable
using Microsoft.Extensions.Hosting;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        using var app = FrogServerHostFactory.CreateHostBuilder(args).Build();
        app.Run();
    }
}
