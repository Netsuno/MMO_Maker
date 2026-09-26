namespace Frog.Core.Enums;

/// <summary>
/// Entrée du catalogue Système : interrupteur nommé ou variable nommée.
/// Les valeurs 0 et 1 sont persistées ; ne pas les réordonner.
/// </summary>
public enum SystemFlagKind : byte
{
    Switch = 0,
    Variable = 1,
}
