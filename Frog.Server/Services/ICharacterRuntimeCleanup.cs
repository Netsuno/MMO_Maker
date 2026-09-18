namespace Frog.Server.Services;

/// <summary>
/// Cancels in-flight character runtime (dialogues, map-event executions, occupants).
/// Implemented by <c>Phase8GameplayHandlers</c>.
/// </summary>
public interface ICharacterRuntimeCleanup
{
    void CancelForCharacter(Guid characterId);
}
