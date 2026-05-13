namespace Frog.Server.Database;

/// <summary>
/// Persistance côté MariaDB : <c>stats</c>, <c>worldFlags</c>, extras dans les tables relationnelles / LONGTEXT ;
/// tant qu’une colonne legacy <c>payload</c> existe, les extras trop gros peuvent encore y être écrits — après **v10**, refus si hors limite KV.
/// </summary>
public interface ICharacterPayloadWriter
{
    /// <summary>Met à jour le JSON assemblé côté lecteur (UTF-8 valide) et les tables relationnelles associées.</summary>
    bool TryUpdatePayloadJson(string characterId, string jsonPayload);
}
