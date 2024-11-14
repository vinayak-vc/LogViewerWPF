using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LogViewerAppWPF {

    public abstract class EncryptDecrypt {
        private const int KeySize = 256; // AES Key size in bits (256 bits)
        private const int BlockSize = 128; // AES block size in bits (128 bits)
        private const string PassPhrase = "DL2J-8PY4-4Q22-GE48-88D2"; // Secret passphrase
        private const int DerivationIterations = 1000; // PBKDF2 iterations for key generation

        public static string Encrypt(string plainText) {
            byte[] saltBytes = GenerateRandomBytes(16); // Use 16 bytes (128 bits) for salt
            byte[] ivBytes = GenerateRandomBytes(16); // Use 16 bytes (128 bits) for IV
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            using (Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(PassPhrase, saltBytes, DerivationIterations)) {
                byte[] keyBytes = password.GetBytes(KeySize / 8);

                using (Aes aes = Aes.Create()) {
                    aes.KeySize = KeySize;
                    aes.BlockSize = BlockSize;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;

                    using (MemoryStream memoryStream = new MemoryStream()) {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.FlushFinalBlock();

                            byte[] cipherBytes = memoryStream.ToArray();
                            return Convert.ToBase64String(saltBytes.Concat(ivBytes).Concat(cipherBytes).ToArray()) + "|END|";
                        }
                    }
                }
            }
        }

        public static string Decrypt(string cipherText) {
            List<string> decryptedLogs = new List<string>();
            // Split by delimiter
            string[] cipherTextArray = cipherText.Split(new[] {
                "|END|"
            }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string encryptedLog in cipherTextArray) {
                byte[] cipherBytesWithSaltAndIv = Convert.FromBase64String(encryptedLog);
                byte[] saltBytes = cipherBytesWithSaltAndIv.Take(16).ToArray(); // Extract salt (16 bytes)
                byte[] ivBytes = cipherBytesWithSaltAndIv.Skip(16).Take(16).ToArray(); // Extract IV (16 bytes)
                byte[] cipherBytes = cipherBytesWithSaltAndIv.Skip(32).ToArray(); // Extract actual cipher text

                using (var password = new Rfc2898DeriveBytes(PassPhrase, saltBytes, DerivationIterations)) {
                    byte[] keyBytes = password.GetBytes(KeySize / 8);

                    using (var aes = Aes.Create()) {
                        aes.KeySize = KeySize;
                        aes.BlockSize = BlockSize;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = keyBytes;
                        aes.IV = ivBytes;

                        using (var memoryStream = new MemoryStream(cipherBytes)) {
                            using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                                byte[] plainTextBytes = new byte[cipherBytes.Length];
                                int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                decryptedLogs.Add(Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount));
                            }
                        }
                    }
                }
            }
            return string.Join(" ", decryptedLogs);
        }

        private static byte[] GenerateRandomBytes(int size) {
            byte[] randomBytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}