using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Détecte les cycles <c>call_common_event</c> entre événements communs publiés.</summary>
public static class CommonEventCycleDetector
{
    public static string? DetectCycles(IReadOnlyList<CommonEventDefinition> events)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var byId = new Dictionary<Guid, CommonEventDefinition>();
        var byAlias = new Dictionary<int, Guid>();
        foreach (var ev in events)
        {
            if (ev.Id == Guid.Empty)
            {
                continue;
            }

            byId[ev.Id] = ev;
            if (ev.EditorAliasId is int alias and > 0)
            {
                byAlias[alias] = ev.Id;
            }
        }

        var adjacency = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var ev in byId.Values)
        {
            var targets = new HashSet<Guid>();
            foreach (var page in ev.Pages)
            {
                CollectCallTargets(page.Commands, byId, byAlias, targets);
            }

            adjacency[ev.Id] = targets;
        }

        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        var stack = new List<Guid>();

        foreach (var id in adjacency.Keys)
        {
            if (visited.Contains(id))
            {
                continue;
            }

            var cycle = Dfs(id, adjacency, byId, visiting, visited, stack);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;
    }

    private static void CollectCallTargets(
        IReadOnlyList<MapEventCommandDefinition> commands,
        IReadOnlyDictionary<Guid, CommonEventDefinition> byId,
        IReadOnlyDictionary<int, Guid> byAlias,
        HashSet<Guid> targets)
    {
        foreach (var command in commands)
        {
            if (string.Equals(
                    command.Discriminator,
                    MapEventCommandDiscriminators.CallCommonEvent,
                    StringComparison.Ordinal))
            {
                if (MapEventParameterSchemas.TryParseCallCommonEvent(
                        command.ParameterJson,
                        out var commonEventId,
                        out var editorAliasId,
                        out _))
                {
                    if (commonEventId != Guid.Empty && byId.ContainsKey(commonEventId))
                    {
                        targets.Add(commonEventId);
                    }
                    else if (editorAliasId is int alias && byAlias.TryGetValue(alias, out var resolved))
                    {
                        targets.Add(resolved);
                    }
                }
            }

            if (string.Equals(command.Discriminator, MapEventCommandDiscriminators.Branch, StringComparison.Ordinal)
                && MapEventParameterSchemas.TryParseBranch(
                    command.ParameterJson,
                    out _,
                    out var thenCmds,
                    out var elseCmds,
                    out _))
            {
                CollectCallTargets(thenCmds, byId, byAlias, targets);
                CollectCallTargets(elseCmds, byId, byAlias, targets);
            }
        }
    }

    private static string? Dfs(
        Guid node,
        IReadOnlyDictionary<Guid, HashSet<Guid>> adjacency,
        IReadOnlyDictionary<Guid, CommonEventDefinition> byId,
        HashSet<Guid> visiting,
        HashSet<Guid> visited,
        List<Guid> stack)
    {
        visiting.Add(node);
        stack.Add(node);

        if (adjacency.TryGetValue(node, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                if (visiting.Contains(next))
                {
                    var cycleStart = stack.IndexOf(next);
                    var cycleIds = stack.Skip(cycleStart).Append(next);
                    var names = cycleIds.Select(id =>
                        byId.TryGetValue(id, out var def) && !string.IsNullOrWhiteSpace(def.Name)
                            ? def.Name
                            : id.ToString("D"));
                    return "Cycle call_common_event détecté: " + string.Join(" -> ", names);
                }

                if (visited.Contains(next))
                {
                    continue;
                }

                var cycle = Dfs(next, adjacency, byId, visiting, visited, stack);
                if (cycle is not null)
                {
                    return cycle;
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        visiting.Remove(node);
        visited.Add(node);
        return null;
    }
}
