using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SantexnikaSRM.Data;

namespace SantexnikaSRM.Services
{
    public sealed class CloudBackupCryptoService
    {
        private static readonly byte[] FileHeader = Encoding.ASCII.GetBytes("OSONBK01");
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const string KeyFileName = "cloud_backup_key.dat";
        private const string ConsentFileName = "cloud_backup_enabled";

        public bool IsEnabled()
        {
            string path = GetConsentPath();
            return File.Exists(path) && string.Equals(File.ReadAllText(path).Trim(), "enabled", StringComparison.Ordinal);
        }

        public string Enable()
        {
            byte[] key;
            string keyPath = GetKeyPath();
            if (File.Exists(keyPath))
            {
                key = ReadProtectedKey();
                File.WriteAllText(GetConsentPath(), "enabled");
                CryptographicOperations.ZeroMemory(key);
                return string.Empty;
            }

            key = RandomNumberGenerator.GetBytes(KeySize);
            WriteProtectedKey(key);
            string recoveryKey = Convert.ToBase64String(key);
            CryptographicOperations.ZeroMemory(key);
            File.WriteAllText(GetConsentPath(), "enabled");
            return recoveryKey;
        }

        public void Disable()
        {
            File.WriteAllText(GetConsentPath(), "disabled");
        }

        public void ImportRecoveryKey(string recoveryKey)
        {
            if (string.IsNullOrWhiteSpace(recoveryKey))
            {
                throw new ArgumentException("Tiklash kaliti kiritilmagan.", nameof(recoveryKey));
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(recoveryKey.Trim());
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Tiklash kaliti noto'g'ri formatda.", nameof(recoveryKey), ex);
            }

            if (key.Length != KeySize)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new ArgumentException("Tiklash kaliti uzunligi noto'g'ri.", nameof(recoveryKey));
            }

            WriteProtectedKey(key);
            CryptographicOperations.ZeroMemory(key);
        }

        public byte[] Encrypt(byte[] plaintext)
        {
            byte[] key = ReadProtectedKey();
            try
            {
                return EncryptPayload(plaintext, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        public static byte[] EncryptPayload(byte[] plaintext, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            ValidateKey(key);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Encrypt(nonce, plaintext, ciphertext, tag, FileHeader);

                byte[] encrypted = new byte[FileHeader.Length + NonceSize + TagSize + ciphertext.Length];
                Buffer.BlockCopy(FileHeader, 0, encrypted, 0, FileHeader.Length);
                Buffer.BlockCopy(nonce, 0, encrypted, FileHeader.Length, NonceSize);
                Buffer.BlockCopy(tag, 0, encrypted, FileHeader.Length + NonceSize, TagSize);
                Buffer.BlockCopy(ciphertext, 0, encrypted, FileHeader.Length + NonceSize + TagSize, ciphertext.Length);
                return encrypted;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(tag);
            }
        }

        public byte[] Decrypt(byte[] encrypted)
        {
            byte[] key = ReadProtectedKey();
            try
            {
                return DecryptPayload(encrypted, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        public static byte[] DecryptPayload(byte[] encrypted, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(encrypted);
            ValidateKey(key);
            int minimumLength = FileHeader.Length + NonceSize + TagSize;
            if (encrypted.Length < minimumLength || !encrypted.AsSpan(0, FileHeader.Length).SequenceEqual(FileHeader))
            {
                throw new InvalidDataException("Bulut zaxira faylining formati noto'g'ri.");
            }

            byte[] nonce = encrypted.AsSpan(FileHeader.Length, NonceSize).ToArray();
            byte[] tag = encrypted.AsSpan(FileHeader.Length + NonceSize, TagSize).ToArray();
            byte[] ciphertext = encrypted.AsSpan(minimumLength).ToArray();
            byte[] plaintext = new byte[ciphertext.Length];

            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, FileHeader);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new InvalidDataException("Bulut zaxirasini ochib bo'lmadi. Tiklash kalitini tekshiring.", ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(tag);
            }
        }

        private static void ValidateKey(byte[] key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length != KeySize)
            {
                throw new ArgumentException("Bulut zaxirasi kaliti 32 bayt bo'lishi kerak.", nameof(key));
            }
        }

        private static byte[] ReadProtectedKey()
        {
            string path = GetKeyPath();
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Bulut zaxirasini shifrlash kaliti topilmadi. Tiklash kalitini import qiling.");
            }

            try
            {
                byte[] key = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
                if (key.Length != KeySize)
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new InvalidDataException("Bulut zaxira kalitining formati noto'g'ri.");
                }

                return key;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Mahalliy bulut zaxira kalitini ochib bo'lmadi. Tiklash kalitini import qiling.", ex);
            }
        }

        private static void WriteProtectedKey(byte[] key)
        {
            byte[] protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
            string keyPath = GetKeyPath();
            string temporaryPath = keyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, protectedKey);
                File.Move(temporaryPath, keyPath, overwrite: true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string GetKeyPath() => Path.Combine(Database.GetAppDataRoot(), KeyFileName);

        private static string GetConsentPath() => Path.Combine(Database.GetAppDataRoot(), ConsentFileName);
    }
}
