// #TODO (FR) : Étendre vers un résultat de validation riche (liste d’erreurs, codes).
#nullable enable
namespace Frog.Core.Interfaces;

public interface IValidatable
{
    bool Validate(out string? errorMessage);
}
