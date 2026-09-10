using Frog.Core.Models;

namespace Frog.Core.Events;

/// <summary>Source d'événements communs pour le planificateur (pas d'I/O réseau/UI dans Core).</summary>
public interface IMapEventCommonEventSource
{
    Task<CommonEventDefinition?> TryGetByIdAsync(Guid commonEventId, CancellationToken cancellationToken = default);

    Task<CommonEventDefinition?> TryGetByAliasAsync(int editorAliasId, CancellationToken cancellationToken = default);
}
