using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Collection unique : tous les smokes UI partagent le même hôte STA (pas de parallélisme).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiSmokeCollectionDefinition : ICollectionFixture<UiSmokeCollectionMarker>
{
    public const string Name = "UiSmoke";
}

public sealed class UiSmokeCollectionMarker : IDisposable
{
    public void Dispose()
    {
        StaTestRunner.ShutdownSuite();
    }
}
