using System.Security.Cryptography;
using System.Text;
using ct.console.common;
using ct.lib;
using ct.lib.extensions;

namespace ct.console.tests
{
    public class CtCryptoExtensionsTests
    {
        [Fact]
        public void GenerateRandomKey_ShouldReturnArrayOfSpecifiedSize()
        {
            // Arrange
            int size = 32;

            // Act
            var result = CtCryptoExtensions.GenerateRandomKey(size);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(size, result.Length);
        }

        [Fact]
        public void AsBase64String_ShouldReturnValidBase64String()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("test-data");

            // Act
            var result = data.AsBase64String();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Convert.ToBase64String(data), result);
        }

        [Fact]
        public async Task Encrypt_Decrypt_ShouldRoundTripSuccessfully()
        {
            // Arrange
            string originalText = "confidential data";
            byte[] data = Encoding.UTF8.GetBytes(originalText);

            // Generate a random key
            var key = CtCryptoExtensions.GenerateRandomKey(32);
            var keyAsString = Convert.ToBase64String(key);

            // Act
            var encryptedData = await data.Encrypt(keyAsString);
            var decryptedData = await encryptedData.DecryptAsync(keyAsString);
            var decryptedText = Encoding.UTF8.GetString(decryptedData);

            // Assert
            Assert.NotNull(encryptedData);
            Assert.NotEmpty(encryptedData);
            Assert.Equal(originalText, decryptedText);
        }

        [Fact]
        public void Encrypt_Generic_ShouldReturnBase64EncodedJson()
        {
            // Arrange
            var obj = new { Id = 1, Name = "TestObject" };
            var key = CtCryptoExtensions.GenerateRandomKey(32);

            // Act
            var encryptedString = obj.Encrypt(key);

            // Assert
            Assert.NotNull(encryptedString);
            Assert.NotEmpty(encryptedString);

            // Additional check: Ensure it's valid Base64
            var base64Decoded = Convert.FromBase64String(encryptedString);
            Assert.NotEmpty(base64Decoded);

            // Ensure the string is not the original JSON format
            var jsonString = System.Text.Json.JsonSerializer.Serialize(obj);
            Assert.DoesNotContain(jsonString, encryptedString);
        }

        [Fact]
        public async Task DecryptAsync_ShouldThrowWithInvalidKey()
        {
            // Arrange
            string originalText = "confidential data";
            byte[] data = Encoding.UTF8.GetBytes(originalText);

            var validKey = CtCryptoExtensions.GenerateRandomKey(32);
            var validKeyAsString = Convert.ToBase64String(validKey);

            var invalidKey = CtCryptoExtensions.GenerateRandomKey(32);
            var invalidKeyAsString = Convert.ToBase64String(invalidKey);

            var encryptedData = await data.Encrypt(validKeyAsString);

            // Act & Assert
            await Assert.ThrowsAsync<CryptographicException>(() => encryptedData.DecryptAsync(invalidKeyAsString));
        }
    }
}