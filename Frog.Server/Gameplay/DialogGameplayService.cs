using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Dialogues typés côté serveur (P8-3).</summary>
public sealed class DialogGameplayService(IPublishedDialogueCatalog dialogues)
{
    private readonly IPublishedDialogueCatalog _dialogues = dialogues;

    public async Task<string?> TryStartDialogueAsync(
        Guid characterId,
        Guid dialogueId,
        CancellationToken cancellationToken = default)
    {
        _ = characterId;
        var definition = await _dialogues.TryGetPublishedByIdAsync(dialogueId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        if (definition.Lines.Count > 0)
        {
            var first = definition.Lines[0];
            var speaker = string.IsNullOrWhiteSpace(first.Speaker) ? string.Empty : $"{first.Speaker}: ";
            return speaker + first.Text;
        }

        if (definition.Choices.Count > 0)
        {
            return definition.Choices[0].Label;
        }

        return definition.Name;
    }
}
