namespace Frog.Core.Enums;

/// <summary>Rôle de guilde vérifié serveur. N'accorde jamais de privilège opérateur.</summary>
public enum GuildRole : byte
{
    Member = 0,
    Officer = 1,
    Leader = 2
}
