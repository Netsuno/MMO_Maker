using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Server.Models;

namespace Frog.Server.Gameplay;

internal static partial class MapEventCommandExecutorExtensions
{
    public static async Task<(IReadOnlyList<MapEventCommandDefinition> Commands, string? Error)> FlattenBranchesAsync(
        this MapEventCommandExecutor executor,
        Session session,
        Guid characterId,
        IReadOnlyList<MapEventCommandDefinition> commands,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        if (depth > MapEventRuntimeLimits.MaxBranchDepth)
        {
            return (Array.Empty<MapEventCommandDefinition>(), "Profondeur de branche excessive.");
        }

        var output = new List<MapEventCommandDefinition>();
        foreach (var command in commands)
        {
            if (command.Discriminator != MapEventCommandDiscriminators.Branch)
            {
                output.Add(command);
                continue;
            }

            if (!MapEventParameterSchemas.TryParseBranch(
                    command.ParameterJson,
                    out var condition,
                    out var thenCommands,
                    out var elseCommands,
                    out var parseErr))
            {
                return (Array.Empty<MapEventCommandDefinition>(), parseErr);
            }

            var pass = await executor.EvaluateConditionAsync(session, characterId, condition, cancellationToken)
                .ConfigureAwait(false);
            var branch = pass ? thenCommands : elseCommands;
            var (nested, nestedErr) = await executor.FlattenBranchesAsync(
                    session,
                    characterId,
                    branch,
                    cancellationToken,
                    depth + 1)
                .ConfigureAwait(false);
            if (nestedErr is not null)
            {
                return (Array.Empty<MapEventCommandDefinition>(), nestedErr);
            }

            output.AddRange(nested);
        }

        return (output, null);
    }
}
