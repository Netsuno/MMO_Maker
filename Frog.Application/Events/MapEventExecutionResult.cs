namespace Frog.Application.Events;

public sealed class MapEventExecutionResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    /// <summary>Texte client si une commande <c>show_text</c> a été exécutée.</summary>
    public string? ShowText { get; init; }

    public bool SwitchesChanged { get; init; }

    public static MapEventExecutionResult Ok(string message, string? showText = null, bool switchesChanged = false) =>
        new()
        {
            Success = true,
            Message = message,
            ShowText = showText,
            SwitchesChanged = switchesChanged,
        };

    public static MapEventExecutionResult Fail(string message) =>
        new() { Success = false, Message = message };
}
