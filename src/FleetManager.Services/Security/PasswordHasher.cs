using System.Security.Cryptography;
using System.Text;

namespace FleetManager.Services.Security;

/// <summary>
/// Хеширование паролей SHA-256 + индивидуальная соль.
/// Формат: SHA256(UTF8(password + salt)) в hex-нижнем регистре.
/// ВАЖНО: для production рекомендуется PBKDF2/Argon2 с большим числом итераций.
/// </summary>
public static class PasswordHasher
{
    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Hash(string password, string salt)
    {
        var data = Encoding.UTF8.GetBytes(password + salt);
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string password, string salt, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash)) return false;
        var actual = Encoding.ASCII.GetBytes(Hash(password, salt));
        var expected = Encoding.ASCII.GetBytes(expectedHash.Trim().ToLowerInvariant());
        return actual.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
