using System;
using System.Security.Cryptography;
using System.Text;

namespace Aire.Services
{
    /// <summary>
    /// Encrypts and decrypts sensitive strings (API keys, passwords, tokens) using
    /// Windows DPAPI (<see cref="ProtectedData"/>). Data is bound to the current
    /// Windows user account — only the same user on the same machine can decrypt it.
    ///
    /// Encrypted values are prefixed with <c>"dpapi:"</c> so legacy plaintext values
    /// already in the database are handled transparently (backward-compatible migration).
    /// </summary>
    public static class SecureStorage
    {
        // App-specific entropy makes it marginally harder for an attacker who has
        // obtained a ProtectedData blob to brute-force the plaintext without also
        // knowing this constant.
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("Aire-SecureStorage-v1");

        private const string Prefix = "dpapi:";

        /// <summary>
        /// Encrypts <paramref name="plainText"/> and returns a Base64-encoded blob
        /// prefixed with <c>"dpapi:"</c>, ready to write to the database.
        /// Returns the original value unchanged when it is <see langword="null"/>,
        /// empty, or already encrypted.
        /// </summary>
        public static string? Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            if (plainText.StartsWith(Prefix, StringComparison.Ordinal)) return plainText;

            var data      = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Decrypts a value previously encrypted with <see cref="Protect"/>.
        /// Returns the value unchanged when it is <see langword="null"/>, empty,
        /// or has no <c>"dpapi:"</c> prefix (legacy plaintext — transparent migration).
        /// Returns the raw ciphertext on decryption failure so the app does not crash;
        /// the user will simply see a garbled key and can re-enter it.
        /// </summary>
        public static string? Unprotect(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal)) return cipherText;

            try
            {
                var encrypted = Convert.FromBase64String(cipherText[Prefix.Length..]);
                var data      = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // Wrong user/machine or corrupted blob — return raw value rather than crashing.
                return cipherText;
            }
        }

        /// <summary>Returns true when the value has the <c>"dpapi:"</c> prefix.</summary>
        public static bool IsProtected(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
