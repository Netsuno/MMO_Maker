using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Dialogues typés côté serveur (P8-3 / P8-R4).</summary>
public sealed class DialogGameplayService(
    IPublishedDialogueCatalog dialogues,
    DialogSessionService sessions)
{
    private readonly IPublishedDialogueCatalog _dialogues = dialogues;
    private readonly DialogSessionService _sessions = sessions;

    public Task<DialogueSessionStart?> TryStartDialogueSessionAsync(
        Guid characterId,
        Guid dialogueId,
        CancellationToken cancellationToken = default) =>
        _sessions.TryStartSessionAsync(characterId, dialogueId, cancellationToken);

    public async Task<string?> TryStartDialogueAsync(
        Guid characterId,
        Guid dialogueId,
        CancellationToken cancellationToken = default)
    {
        var started = await TryStartDialogueSessionAsync(characterId, dialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (started is null)
        {
            return null;
        }

        var speaker = string.IsNullOrWhiteSpace(started.Speaker) ? string.Empty : $"{started.Speaker}: ";
        return speaker + started.Text;
    }
}
