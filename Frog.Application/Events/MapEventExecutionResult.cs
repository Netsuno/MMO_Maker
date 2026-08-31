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
        DialogueStatePushWire? dialogueState = null) =>
        new()
        {
            Success = true,
            Message = message,
            ShowText = showText,
            SwitchesChanged = switchesChanged,
            VariablesChanged = variablesChanged,
            InventoryChanged = inventoryChanged,
            GoldChanged = goldChanged,
            TeleportApplied = teleportApplied,
            DialogueSummary = dialogueSummary,
            QuestSummary = questSummary,
            DialogueState = dialogueState,
        };

    public static MapEventExecutionResult Fail(string message) =>
        new() { Success = false, Message = message };
}
