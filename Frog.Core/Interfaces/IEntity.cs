// #TODO (FR) : Clarifier le modèle d’identité commun (Id/Name immuables ?).
#nullable enable
namespace Frog.Core.Interfaces;

/// <summary>Contrat d’entité nommée avec identifiant numérique.</summary>
public interface IEntity
{
    int Id { get; }
    string Name { get; }
}
