using System.Text.Json;

namespace ct.lib.extensions;

public static class CtIoExtensions
{
    public static async Task<string?> AsEncryptedText(this Stream requestStream)
    {
        using var reader = new StreamReader(requestStream);
        var result = await reader.ReadToEndAsync();
        result = JsonSerializer.Deserialize<string>(result);
        return result;
    }
    
    public static IEnumerable<string> GetFilesToShare(string extensionFilter, string directoryToShare)
    {
        var files = Directory.GetFiles(directoryToShare, $"*.{extensionFilter}");
        return files;
    }
    
    public static void CreateBlankFile(string filePath, long fileLength)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.SetLength(fileLength);
    }

    public static async Task<string> ComputeHash(string filePath, long start, long end)
    {
        await using var fs = File.OpenRead(filePath);
        
        var length = end - start;
        fs.Seek(start, SeekOrigin.Begin);
        
        var buffer = new byte[length];
        var bytesRead = await fs.ReadAsync(buffer.AsMemory(0, (int)length));
        
        if (bytesRead < length)
        {
            Array.Resize(ref buffer, bytesRead);
        }
        
        // Compute the hash
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(buffer);

        // Return the hash as a Base64-encoded string
        return Convert.ToBase64String(hash);
    }
    
    public static void WriteBytes(string filePath, byte[] partBytes, long offset)
    {
        if (partBytes == null)
            throw new ArgumentNullException(nameof(partBytes));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be a non-negative value.");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"The specified file does not exist: {filePath}");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
        
        fs.Seek(offset, SeekOrigin.Begin);  
        fs.Write(partBytes, 0, partBytes.Length);  
    }

    public static IEnumerable<string> GetFoldersToShare(string extensionFilter, string directoryToShare)
    {
        var folders = Directory.GetDirectories(directoryToShare);
        return folders;
    }
    
    public static void Dump<T>(this T data, string outputPath)
    {
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"File {outputPath} was saved");
    }
    
    public static T Load<T>(this string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            throw new FileNotFoundException($"The specified file was not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("Deserialization failed or returned null.");
    }
    
    public static string ToHumanReadableSize(this long byteSize)
    {
        string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

        if (byteSize <= 0) return "Zero";

        int sizeIndex = (int)Math.Log(byteSize, 1024);
        sizeIndex = Math.Min(sizeIndex, sizeSuffixes.Length - 1); // Safety check for array bounds

        double readableSize = byteSize / Math.Pow(1024, sizeIndex);

        return $"{readableSize:0.##} {sizeSuffixes[sizeIndex]}";
    }
}