namespace Frog.Application.Identity;

/// <summary>Règles communes username / mot de passe (Phase 7.1).</summary>
public static class AccountInputRules
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 32;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var trimmed = username.Trim();
        if (trimmed.Length is < MinUsernameLength or > MaxUsernameLength)
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static bool IsValidPassword(string? password)
        => !string.IsNullOrEmpty(password)
           && password.Length is >= MinPasswordLength and <= MaxPasswordLength;

    public static bool IsValidLoginPassword(string? password)
        => !string.IsNullOrEmpty(password) && password.Length <= MaxPasswordLength;
}
