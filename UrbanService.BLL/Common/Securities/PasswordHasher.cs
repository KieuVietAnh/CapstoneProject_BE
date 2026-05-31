using System.Security.Cryptography;

namespace UrbanService.BLL.Common.Securities
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string hashed)
        {
            var parts = hashed.Split('.');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var actual = pbkdf2.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
    }
}
