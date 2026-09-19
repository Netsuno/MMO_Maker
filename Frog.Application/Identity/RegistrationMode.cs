namespace Frog.Application.Identity;

/// <summary>
/// Inscriptions TCP. Bêta fermée = <see cref="ProvisionedOnly"/> (Register refusé).
/// <see cref="InviteOnly"/> : jalon — pas de jetons d'invitation ; TCP refusé comme ProvisionedOnly.
/// </summary>
public enum RegistrationMode
{
    Open = 0,
    InviteOnly = 1,
    ProvisionedOnly = 2,
}
