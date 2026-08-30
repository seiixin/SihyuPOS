#nullable enable
namespace SihyuPOSPayroll.Helpers
{
    /// <summary>
    /// Thin wrapper around BCrypt so call sites don't import BCrypt.Net directly.
    /// Work factor 12 is the project default (~250 ms on modern hardware).
    /// </summary>
    public static class PasswordHasher
    {
        private const int WorkFactor = 12;

        /// <summary>Returns a BCrypt hash of <paramref name="plainText"/>.</summary>
        public static string Hash(string plainText) =>
            BCrypt.Net.BCrypt.HashPassword(plainText, WorkFactor);

        /// <summary>
        /// Returns <c>true</c> when <paramref name="plainText"/> matches
        /// <paramref name="hash"/>; <c>false</c> otherwise (including on any error).
        /// </summary>
        public static bool Verify(string plainText, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(plainText, hash);
            }
            catch
            {
                // Malformed hash in DB — treat as mismatch, never throw.
                return false;
            }
        }
    }
}
