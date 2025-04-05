namespace ct.console.common;

public static class CtArgsExtensions
{
    public static string GetFileExtensionFilter(this string[] args)
    {
        var fileExt = args.FirstOrDefault(arg => arg.StartsWith("--file-ext="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(fileExt) ? fileExt : "*.*";
        
        return Reported(retVal, nameof(fileExt));
    }

    public static CtMode GetMode(this string[] args)
    {
        var mode = args.FirstOrDefault(arg => arg.StartsWith("--mode="))?.Split('=')[1];
        var retVal = Enum.TryParse<CtMode>(mode, out var m) ? m : CtMode.Server;
        
        return Reported(retVal, nameof(mode));
    }

    public static int GetChunkSize(this string[] args)
    {
        var chunkSize = args.FirstOrDefault(arg => arg.StartsWith("--chunk-size="))?.Split('=')[1];
        var retVal = int.TryParse(chunkSize, out var cz) ? cz : 1024 * 1024 * 10;
        
        return Reported(retVal, nameof(chunkSize));
    }

    public static int GetThreadsCount(this string[] args)
    {
        var threads = args.FirstOrDefault(arg => arg.StartsWith("--threads="))?.Split('=')[1];
        var retVal = int.TryParse(threads, out var t) ? t : 2;
        
        return Reported(retVal, nameof(threads));
    }

    public static string GetChunkMapPath(this string[] args)
    {
        var chunkMap = args.FirstOrDefault(arg => arg.StartsWith("--chunk-map="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(chunkMap) ? chunkMap : Path.Combine(AppContext.BaseDirectory, "chunkMap.json");
        
        return Reported(retVal, nameof(chunkMap));
    }

    public static string GetServerUrl(this string[] args)
    {
        var serverUrl = args.FirstOrDefault(arg => arg.StartsWith("--server-url="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(serverUrl) ? serverUrl : throw new ArgumentException("Server url is not specified");

        return Reported(retVal, nameof(serverUrl));
    }
    
    public static string GetEncryptionKey()
    {
        const string encryptionKeyFilePath = "enc.key";

        if (!File.Exists(encryptionKeyFilePath))
        {
            throw new FileNotFoundException($"Encryption key file not found: {encryptionKeyFilePath}");
        }

        var encryptionKey = File.ReadAllText(encryptionKeyFilePath).Trim();

        if (string.IsNullOrEmpty(encryptionKey))
        {
            throw new ArgumentException("Encryption key file is empty.");
        }

        return encryptionKey;
    }


    private static T Reported<T>(this T data, string parameterName)
    {
        Console.WriteLine($"{parameterName}:{data}");
        return data;
    }
}