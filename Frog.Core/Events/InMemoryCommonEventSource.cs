using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Catalogue common-event en mémoire pour planification et tests.</summary>
public sealed class InMemoryCommonEventSource : IMapEventCommonEventSource
{
    public static InMemoryCommonEventSource Empty { get; } = new(Array.Empty<CommonEventDefinition>());

    private readonly Dictionary<Guid, CommonEventDefinition> _byId = new();
    private readonly Dictionary<int, CommonEventDefinition> _byAlias = new();

    public InMemoryCommonEventSource(IEnumerable<CommonEventDefinition> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var ev in events)
        {
            if (ev.Id == Guid.Empty)
            {
                continue;
            }

            _byId[ev.Id] = ev;
            if (ev.EditorAliasId is int alias and > 0)
            {
                _byAlias[alias] = ev;
            }
        }
    }

    public Task<CommonEventDefinition?> TryGetByIdAsync(
        Guid commonEventId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _byId.TryGetValue(commonEventId, out var ev);
        return Task.FromResult<CommonEventDefinition?>(ev);
    }

    public Task<CommonEventDefinition?> TryGetByAliasAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _byAlias.TryGetValue(editorAliasId, out var ev);
        return Task.FromResult<CommonEventDefinition?>(ev);
    }
}
