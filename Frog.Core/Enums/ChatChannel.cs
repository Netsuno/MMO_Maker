namespace Frog.Core.Enums;

/// <summary>Canal d'envoi pour les messages chat (global, carte courante, chuchotement).</summary>
public enum ChatChannel : byte
{
    Global = 0,
    Map = 1,
    Whisper = 2
}
