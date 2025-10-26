// #TODO (FR) : Étendre vers un résultat de validation riche (liste d’erreurs, codes).
#nullable enable
namespace Frog.Core.Interfaces;

/// <summary>Contrat pour les objets pouvant se valider eux‑mêmes.</summary>
public interface IValidatable
{
    bool Validate(out string? error);
}
