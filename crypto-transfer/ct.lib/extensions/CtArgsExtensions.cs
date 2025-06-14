using ct.lib.model;

namespace ct.lib.extensions;

public static class CtArgsExtensions
{
    public static string GetFileExtensionFilter(this string[] args)
    {
        var fileExt = args.FirstOrDefault(arg => arg.StartsWith("--file-ext="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(fileExt) ? fileExt : "*.*";
        
        return Reported(retVal, nameof(fileExt));
    }
    
    public static string GetDirectoryToShare(this string[] args)
    {
        var dirToShare = args.FirstOrDefault(arg => arg.StartsWith("--dir-to-share="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(dirToShare) ? dirToShare : throw new ArgumentException("Directory to share is not specified");
        
        return Reported(retVal, nameof(dirToShare));
    }
    
    public static string GetOutput(this string[] args)
    {
        var output = args.FirstOrDefault(arg => arg.StartsWith("--output="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(output) ? output : throw new ArgumentException("Output is not specified");
        
        return Reported(retVal, nameof(output));
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

    public static bool ReuseEncryptionKey(this string[] args)
    {
        var encKeyPath = args.FirstOrDefault(arg => arg.StartsWith("--key-path="))?.Split('=')[1];

        if (File.Exists(encKeyPath))
        {
            return true;
        }
        
        return false;
    }
    
    public static string GetEncryptionKey(this string[] args)
    {
        var encKeyPath = args.FirstOrDefault(arg => arg.StartsWith("--enc-key="))?.Split('=')[1];
        
        if (!File.Exists(encKeyPath))
        {
            throw new FileNotFoundException($"Encryption key file not found: {encKeyPath}");
        }

        var encryptionKey = encKeyPath.Load<string>();

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