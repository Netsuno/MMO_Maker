#nullable enable
using Frog.Application.Playtest;
using Microsoft.Extensions.Hosting;

namespace Frog.Server;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        if (PlaytestChildEnvironment.IsPlaytestChildProcess()
            && PlaytestChildEnvironment.TryFailFastIfForbiddenPresent(Console.Error, out var exitCode))
        {
            Environment.Exit(exitCode);
            return;
        }

        using var app = FrogServerHostFactory.CreateHostBuilder(args).Build();
        app.Run();
    }
}
