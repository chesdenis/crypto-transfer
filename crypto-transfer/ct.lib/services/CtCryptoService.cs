using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ct.lib.services
{
    
    public interface ICtCryptoService
    {
        Task<byte[]> EncryptBytesAsync(byte[] data, string keyAsString);
        Task<string> EncryptObjectAsync<T>(T data, string keyAsString);
        Task<T> DecryptObjectAsync<T>(string data, string keyAsString);
        Task<byte[]> DecryptAsync(byte[] data, string keyAsString);
    }
    
    public class CtCryptoService(ILogger<CtCryptoService> logger) : ICtCryptoService
    {
        public async Task<byte[]> EncryptBytesAsync(byte[] data, string keyAsString)
        {
            logger.LogInformation("Encrypting {Length} bytes using provided Base64 key", data.Length);

            var key = Convert.FromBase64String(keyAsString);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            
            using var memoryStream = new MemoryStream();
            await memoryStream.WriteAsync(aes.IV.AsMemory(0, aes.IV.Length));

            await using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                await cryptoStream.WriteAsync(data);
            }

            var encryptedData = memoryStream.ToArray();
            logger.LogInformation("Encryption of {Length} bytes completed successfully", data.Length);

            return encryptedData;
        }

        public async Task<string> EncryptObjectAsync<T>(T data, string keyAsString)
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(data);
            var jsonAsBytes = Encoding.UTF8.GetBytes(jsonString);
            
            var encryptedBytes = await EncryptBytesAsync(jsonAsBytes, keyAsString);
            var encryptedResult = Convert.ToBase64String(encryptedBytes);
            
            return encryptedResult;
        }

        public async Task<T> DecryptObjectAsync<T>(string data, string keyAsString)
        {
            var encryptedBytes = Convert.FromBase64String(data);
            var decryptedBytes = await DecryptAsync(encryptedBytes, keyAsString);
            var jsonString = Encoding.UTF8.GetString(decryptedBytes);

            var decryptObjectAsync = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
            
            return decryptObjectAsync ?? throw new InvalidOperationException();
        }

        public async Task<byte[]> DecryptAsync(byte[] data, string keyAsString)
        {
            logger.LogInformation("Decrypting {Length} bytes using provided Base64 key", data.Length);

            var key = Convert.FromBase64String(keyAsString);

            using var aes = Aes.Create();
            aes.Key = key;

            var ivLength = aes.BlockSize / 8;
            var iv = data.Take(ivLength).ToArray();
            var encryptedData = data.Skip(ivLength).ToArray();

            aes.IV = iv;
            
            using var memoryStream = new MemoryStream();
            await using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                await cryptoStream.WriteAsync(encryptedData);
            }

            var decryptedData = memoryStream.ToArray();

            logger.LogInformation("Decryption {Length} bytes completed successfully", data.Length);

            return decryptedData;
        }
    }
}