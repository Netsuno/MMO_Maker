using System.Security.Cryptography;
using System.Text;
using Frog.Core.Utils;

namespace Frog.Core.Security;

/// <summary>
/// Hachage mot de passe modernisable : PBKDF2-SHA256 (v1) + compatibilité legacy SHA256+sel.
/// </summary>
public static class PasswordHasher
{
    private const int Pbkdf2Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const string V1Prefix = "$frog-v1$pbkdf2-sha256$";

    public static string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);
        return $"{V1Prefix}{Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool VerifyPassword(string password, string storedHash, string? legacySaltBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        if (storedHash.StartsWith(V1Prefix, StringComparison.Ordinal))
        {
            return VerifyV1(password, storedHash);
        }

        if (!string.IsNullOrWhiteSpace(legacySaltBase64))
        {
            return HashHelper.VerifyPassword(password, storedHash, legacySaltBase64);
        }

        return false;
    }

    /// <summary>Vérifie avec délai constant même si le compte est absent.</summary>
    public static bool VerifyOrTimingSafeReject(string password, string? storedHash, string? legacySaltBase64)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            // Burn comparable work without leaking account existence through timing.
            _ = HashPassword(password);
            return false;
        }

        return VerifyPassword(password, storedHash, legacySaltBase64);
    }

    private static bool VerifyV1(string password, string stored)
    {
        // $frog-v1$pbkdf2-sha256$600000$<salt>$<key>
        var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5
            || !parts[0].Equals("frog-v1", StringComparison.Ordinal)
            || !parts[1].Equals("pbkdf2-sha256", StringComparison.Ordinal)
            || !int.TryParse(parts[2], out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
