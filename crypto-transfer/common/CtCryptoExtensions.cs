using System.Security.Cryptography;
using System.Text;

namespace ct.console.common;

public static class CtCryptoExtensions
{
    public static byte[] GenerateRandomKey(int size)
    {
        var key = new byte[size];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }

    public static string AsBase64String(this byte[] data) => Convert.ToBase64String(data);

    public static async Task<byte[]> Encrypt(this byte[] data, string keyAsString)
    {
        var key = Convert.FromBase64String(keyAsString);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // Create a random IV for this encryption

        using var memoryStream = new MemoryStream();
        // Write the IV at the start of the encrypted result
        await memoryStream.WriteAsync(aes.IV.AsMemory(0, aes.IV.Length));

        await using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            await cryptoStream.WriteAsync(data);
        }

        return memoryStream.ToArray();
    }
    
    public static string Encrypt<T>(this T data, byte[] key)
    {
        var jsonString = System.Text.Json.JsonSerializer.Serialize(data);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        var iv = aes.IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, iv);
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(jsonString);
        }
        var encrypted = msEncrypt.ToArray();
        var result = new { IV = Convert.ToBase64String(iv), Data = Convert.ToBase64String(encrypted) };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(result)));
    }
}