using Frog.Application.Identity;

namespace Frog.Server.Config;

/// <summary>Section <c>Registration:Mode</c> = Open | InviteOnly | ProvisionedOnly.</summary>
public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public RegistrationMode Mode { get; set; } = RegistrationMode.Open;

    public bool AllowsTcpRegister => Mode == RegistrationMode.Open;
}
