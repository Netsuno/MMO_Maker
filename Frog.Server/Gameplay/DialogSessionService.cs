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
        CancellationToken cancellationToken = default)
    {
        var definition = await _dialogues.TryGetPublishedByIdAsync(dialogueId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var revision = await _dialogues.TryGetPublishedRevisionByIdAsync(dialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (revision is null or <= 0)
        {
            return null;
        }

        return TryStartSessionCore(characterId, dialogueId, definition, revision.Value);
    }

    internal DialogueSessionStart? TryStartSessionCore(
        Guid characterId,
        Guid dialogueId,
        DialogueDefinition definition,
        long publishedRevision)
    {
        var token = RandomNumberGenerator.GetBytes(Phase8Wire.DialogueSessionTokenBytes);
        var tokenKey = Convert.ToHexString(token);
        var boundChoices = definition.Choices
            .Select(c => new DialogueChoiceDefinition
            {
                ChoiceId = c.ChoiceId,
                Label = c.Label,
                StartQuestId = c.StartQuestId,
            })
            .ToList();
        var session = new DialogueSession
        {
            CharacterId = characterId,
            DialogueId = dialogueId,
            PublishedRevision = publishedRevision,
            Token = token,
            ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(10),
            BoundSpeaker = definition.Lines.Count > 0 ? definition.Lines[0].Speaker : string.Empty,
            BoundText = definition.Lines.Count > 0
                ? definition.Lines[0].Text
                : boundChoices.Count > 0
                    ? boundChoices[0].Label
                    : definition.Name,
            BoundChoices = boundChoices,
        };
        _sessions[tokenKey] = session;

        var choices = boundChoices
            .Select(c => new DialogueChoiceWire { ChoiceId = c.ChoiceId, Label = c.Label })
            .ToList();
        return new DialogueSessionStart(token, publishedRevision, session.BoundSpeaker, session.BoundText, choices);
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
        var tokenKey = Convert.ToHexString(sessionToken);
        if (!_sessions.TryGetValue(tokenKey, out var session))
        {
            return DialogueChoiceResult.Rejected("Session dialogue expirée ou inconnue.");
        }

        lock (session.Sync)
        {
            if (session.Consumed)
            {
                return DialogueChoiceResult.Rejected("Session dialogue déjà consommée.");
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

            var choice = session.BoundChoices.FirstOrDefault(c => c.ChoiceId == choiceId);
            if (choice is null)
            {
                return DialogueChoiceResult.Rejected("Choix invalide.");
            }

            session.Consumed = true;
        }

        var currentRevision = await _dialogues.TryGetPublishedRevisionByIdAsync(session.DialogueId, cancellationToken)
            .ConfigureAwait(false);
        if (currentRevision != session.PublishedRevision)
        {
            _sessions.TryRemove(tokenKey, out _);
            return DialogueChoiceResult.Rejected("Dialogue republié — session expirée.");
        }

        _sessions.TryRemove(tokenKey, out _);

        var boundChoice = session.BoundChoices.First(c => c.ChoiceId == choiceId);
        string? questMessage = null;
        if (boundChoice.StartQuestId is Guid questId)
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

        return DialogueChoiceResult.Ok(boundChoice.Label, questMessage);
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
    long PublishedRevision,
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

    public required string BoundSpeaker { get; init; }

    public required string BoundText { get; init; }

    public required IReadOnlyList<DialogueChoiceDefinition> BoundChoices { get; init; }

    public bool Consumed { get; set; }

    public object Sync { get; } = new();
}
