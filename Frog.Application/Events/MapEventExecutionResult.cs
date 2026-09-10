using Frog.Core.Protocol;

namespace Frog.Application.Events;

public sealed class MapEventExecutionResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Texte client si une commande <c>show_text</c> a été exécutée.</summary>
    public string? ShowText { get; init; }

    public bool SwitchesChanged { get; init; }

    public bool VariablesChanged { get; init; }

    public bool InventoryChanged { get; init; }

    public bool GoldChanged { get; init; }

    public bool QuestsChanged { get; init; }

    public bool ProfessionsChanged { get; init; }

    public bool RecipesChanged { get; init; }

    public bool TeleportApplied { get; init; }

    public string? DialogueSummary { get; init; }

    public string? QuestSummary { get; init; }

    public DialogueStatePushWire? DialogueState { get; init; }

    public static MapEventExecutionResult Ok(
        string message,
        string? showText = null,
        bool switchesChanged = false,
        bool variablesChanged = false,
        bool inventoryChanged = false,
        bool goldChanged = false,
        bool teleportApplied = false,
        string? dialogueSummary = null,
        string? questSummary = null,
        DialogueStatePushWire? dialogueState = null,
        bool questsChanged = false,
        bool professionsChanged = false,
        bool recipesChanged = false) =>
        new()
        {
            Success = true,
            Message = message,
            ShowText = showText,
            SwitchesChanged = switchesChanged,
            VariablesChanged = variablesChanged,
            InventoryChanged = inventoryChanged,
            GoldChanged = goldChanged,
            QuestsChanged = questsChanged,
            ProfessionsChanged = professionsChanged,
            RecipesChanged = recipesChanged,
            TeleportApplied = teleportApplied,
            DialogueSummary = dialogueSummary,
            QuestSummary = questSummary,
            DialogueState = dialogueState,
        };

    /// <summary>
    /// Mappe le snapshot persisté (J4-PG) vers le résultat public. <c>teleport</c> /
    /// <c>start_dialogue</c> restent hors TX et ne sont donc jamais présents ici.
    /// </summary>
    public static MapEventExecutionResult FromMutationSnapshot(
        string fallbackMessage,
        MapEventExecutionSnapshot? snap) =>
        Ok(
            message: snap?.ShowText ?? fallbackMessage,
            showText: snap?.ShowText,
            switchesChanged: snap?.SwitchesChanged ?? false,
            variablesChanged: snap?.VariablesChanged ?? false,
            inventoryChanged: snap?.InventoryChanged ?? false,
            goldChanged: snap?.GoldChanged ?? false,
            questSummary: snap?.QuestSummary,
            questsChanged: snap?.QuestsChanged ?? false,
            professionsChanged: snap?.ProfessionsChanged ?? false,
            recipesChanged: snap?.RecipesChanged ?? false);

    public static MapEventExecutionResult Fail(string message) =>
        new() { Success = false, Message = message };
}
