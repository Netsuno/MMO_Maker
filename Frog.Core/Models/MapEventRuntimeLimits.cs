namespace Frog.Core.Models;

/// <summary>Limites runtime documentées (P8-2 appliquera l'interpréteur).</summary>
public static class MapEventRuntimeLimits
{
    public const int MaxPagesPerEvent = 50;
    public const int MaxConditionsPerPage = 32;
    public const int MaxCommandsPerPage = 64;
    public const int MaxBranchDepth = 8;
    public const int MaxCommonEventRecursionDepth = 4;
    public const int MaxExecutionSteps = 256;
    public const int MaxWaitMs = 60_000;
    public const int MaxActiveExecutionsPerCharacter = 4;
    public const int MaxRouteWaypoints = 32;
    public const int MaxConditionParameterBytes = 4096;
    public const int MaxCommandParameterBytes = 8192;
}
