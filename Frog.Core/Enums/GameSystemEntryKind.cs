namespace Frog.Core.Enums;

/// <summary>
/// Ligne du catalogue Système (Données de jeu).
/// Les interrupteurs et variables portent la clé utilisée par les commandes d’événement.
/// Les options du projet sont une ligne unique (nom du jeu, musique de départ).
/// </summary>
public enum GameSystemEntryKind : byte
{
    Switch = 1,
    Variable = 2,
    Options = 3,
}
