using System.Security.Cryptography;

namespace KodisApi.Services
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 hashing for the optional view/edit passwords on a
    /// notebook. Both passwords of one notebook share a salt, which is stored
    /// on the notebook row alongside the hashes.
    /// </summary>
    public sealed class NotebookPasswordHasher
    {
        private const int SaltSizeInBytes = 16;
        private const int HashSizeInBytes = 32;
        private const int Iterations = 210_000;

        public string CreateSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSizeInBytes));

        public string Hash(string password, string salt) =>
            Convert.ToBase64String(Derive(password, salt));

        /// <summary>
        /// Compares in constant time so that a wrong password cannot be
        /// narrowed down by timing the response.
        /// </summary>
        public bool Verify(string? password, string? expectedHash, string? salt)
        {
            if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(salt))
            {
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(expectedHash);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(Derive(password, salt), expected);
        }

        private static byte[] Derive(string password, string salt) =>
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                Convert.FromBase64String(salt),
                Iterations,
                HashAlgorithmName.SHA256,
                HashSizeInBytes);
    }
}
