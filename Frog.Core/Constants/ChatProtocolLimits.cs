namespace Frog.Core.Constants;

/// <summary>Limites partagées client/serveur pour les paquets chat et noms d'utilisateur.</summary>
public static class ChatProtocolLimits
{
    public const int MaxMessageUtf8Bytes = 512;
    public const int MaxUsernameUtf8Bytes = 64;
}
