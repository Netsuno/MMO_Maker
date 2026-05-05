namespace Frog.Server.Models;

public sealed class Session
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public DateTime ConnectedUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    /// <summary>Centre joueur en pixels monde (voir Frog.Core.Constants.WorldMetrics, taille de tuile).</summary>
    public int PixelX { get; set; }

    public int PixelY { get; set; }

    /// <summary>Carte monde courante (monde unique au debut ; instances plus tard).</summary>
    public int CurrentMapId { get; set; } = 1;

    /// <summary><c>frog_character.id</c> (UUID texte) du perso par défaut, rempli après login.</summary>
    public string? CharacterId { get; set; }
}
