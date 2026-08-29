using System.Collections.Concurrent;
using System.Security.Cryptography;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>Sessions dialogue autoritaires avec tokens opaque (P8-R4).</summary>
public sealed class DialogSessionService
{
    private readonly IPublishedDialogueCatalog _dialogues;
    private readonly QuestGameplayService _quests;
    private readonly ConcurrentDictionary<string, DialogueSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public DialogSessionService(
        IPublishedDialogueCatalog dialogues,
        QuestGameplayService quests,
        TimeProvider? clock = null)
    {
        _dialogues = dialogues;
        _quests = quests;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DialogueSessionStart?> TryStartSessionAsync(
        Guid characterId,
        Guid dialogueId,
        long publishedRevision,
        CancellationToken cancellationToken = default)
    {
        var definition = await _dialogues.TryGetPublishedByIdAsync(dialogueId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var token = RandomNumberGenerator.GetBytes(Phase8Wire.DialogueSessionTokenBytes);
        var tokenKey = Convert.ToHexString(token);
        var session = new DialogueSession
        {
            CharacterId = characterId,
            DialogueId = dialogueId,
            PublishedRevision = publishedRevision,
            Token = token,
            ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(10),
            UsedChoiceIds = new HashSet<string>(StringComparer.Ordinal),
        };
        _sessions[tokenKey] = session;

        var speaker = definition.Lines.Count > 0 ? definition.Lines[0].Speaker : string.Empty;
        var text = definition.Lines.Count > 0
            ? definition.Lines[0].Text
            : definition.Choices.Count > 0
                ? definition.Choices[0].Label
                : definition.Name;
        var choices = definition.Choices
            .Select(c => new DialogueChoiceWire { ChoiceId = c.ChoiceId, Label = c.Label })
            .ToList();
        return new DialogueSessionStart(token, speaker, text, choices);
    }

    public Task<DialogueChoiceResult?> TryChooseAsync(
        Guid characterId,
        byte[] sessionToken,
        string choiceId,
        CancellationToken cancellationToken = default)
    {
        if (sessionToken.Length != Phase8Wire.DialogueSessionTokenBytes)
        {
            return Task.FromResult<DialogueChoiceResult?>(DialogueChoiceResult.Rejected("Token session invalide."));
        }

        return TryChooseCoreAsync(characterId, sessionToken, choiceId, cancellationToken);
    }

    private async Task<DialogueChoiceResult?> TryChooseCoreAsync(
        Guid characterId,
        byte[] sessionToken,
        string choiceId,
        CancellationToken cancellationToken)
    {
        if (sessionToken.Length != Phase8Wire.DialogueSessionTokenBytes)
        {
            return DialogueChoiceResult.Rejected("Token session invalide.");
        }

        var tokenKey = Convert.ToHexString(sessionToken);
        if (!_sessions.TryGetValue(tokenKey, out var session))
        {
            return DialogueChoiceResult.Rejected("Session dialogue expirée ou inconnue.");
        }

        if (session.CharacterId != characterId)
        {
            return DialogueChoiceResult.Rejected("Session dialogue appartient à un autre personnage.");
        }

        if (_clock.GetUtcNow() > session.ExpiresAtUtc)
        {
            _sessions.TryRemove(tokenKey, out _);
            return DialogueChoiceResult.Rejected("Session dialogue expirée.");
        }

        if (session.UsedChoiceIds.Contains(choiceId))
        {
            return DialogueChoiceResult.Rejected("Choix déjà utilisé.");
        }

        var definition = await _dialogues.TryGetPublishedByIdAsync(session.DialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return DialogueChoiceResult.Rejected("Dialogue introuvable.");
        }

        var choice = definition.Choices.FirstOrDefault(c => c.ChoiceId == choiceId);
        if (choice is null)
        {
            return DialogueChoiceResult.Rejected("Choix invalide.");
        }

        session.UsedChoiceIds.Add(choiceId);
        string? questMessage = null;
        if (choice.StartQuestId is Guid questId)
        {
            questMessage = await _quests.TryStartQuestAsync(characterId, questId, cancellationToken)
                .ConfigureAwait(false);
        }

        await _quests.NotifyObjectiveProgressAsync(
                characterId,
                QuestObjectiveKind.Talk,
                new QuestObjectiveSignal(DialogueId: session.DialogueId),
                cancellationToken)
            .ConfigureAwait(false);

        return DialogueChoiceResult.Ok(choice.Label, questMessage);
    }

    public void CancelForCharacter(Guid characterId)
    {
        foreach (var kv in _sessions)
        {
            if (kv.Value.CharacterId == characterId)
            {
                _sessions.TryRemove(kv.Key, out _);
            }
        }
    }
}

public sealed record DialogueSessionStart(
    byte[] SessionToken,
    string Speaker,
    string Text,
    IReadOnlyList<DialogueChoiceWire> Choices);

public sealed record DialogueChoiceResult(bool Success, string Message, string? QuestMessage = null)
{
    public static DialogueChoiceResult Ok(string message, string? questMessage) =>
        new(true, message, questMessage);

    public static DialogueChoiceResult Rejected(string message) => new(false, message);
}

internal sealed class DialogueSession
{
    public required Guid CharacterId { get; init; }

    public required Guid DialogueId { get; init; }

    public required long PublishedRevision { get; init; }

    public required byte[] Token { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required HashSet<string> UsedChoiceIds { get; init; }
}
