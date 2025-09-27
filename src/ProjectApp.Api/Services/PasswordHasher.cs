using System.Security.Cryptography;
using System.Text;

namespace ProjectApp.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);
        return $"pbkdf2:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            var parts = hash.Split(':');
            if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var key = pbkdf2.GetBytes(expected.Length);
            return CryptographicOperations.FixedTimeEquals(key, expected);
        }
        catch { return false; }
    }
}
