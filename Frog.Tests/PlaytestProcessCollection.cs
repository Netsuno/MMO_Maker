using Xunit;

namespace Frog.Tests;

/// <summary>Process-spawning playtest suites must not run concurrently (port / log races).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlaytestProcessCollectionDefinition
{
    public const string Name = "PlaytestProcess";
}
