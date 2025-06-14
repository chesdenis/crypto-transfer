using System.Text.Json;
using ct.lib.model;

namespace ct.lib.extensions;

public static class CtJsonExtensions
{
    public static async Task<T?> As<T>(this Stream requestStream) where T : class
    {
       return await JsonSerializer.DeserializeAsync<T>(requestStream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}

public static class CtArgsExtensions
{
    public static string GetFileExtensionFilter(this string[] args)
    {
        var fileExt = args.FirstOrDefault(arg => arg.StartsWith("--file-ext="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(fileExt) ? fileExt : "*.*";

        return retVal;
    } 
    
    public static string GetTargetFile(this string[] args)
    {
        var fileExt = args.FirstOrDefault(arg => arg.StartsWith("--target-file="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(fileExt) ? fileExt : "*.*";

        return retVal;
    }

    public static string GetDirectoryToShare(this string[] args)
    {
        var dirToShare = args.FirstOrDefault(arg => arg.StartsWith("--dir-to-share="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(dirToShare)
            ? dirToShare
            : throw new ArgumentException("--dir-to-share= is not specified");

        return retVal;
    }

    public static string GetOutput(this string[] args)
    {
        var output = args.FirstOrDefault(arg => arg.StartsWith("--output="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(output) ? output : throw new ArgumentException("Output is not specified");

        return retVal;
    }

    public static CtMode GetMode(this string[] args)
    {
        var mode = args.FirstOrDefault(arg => arg.StartsWith("--mode="))?.Split('=')[1];
        var retVal = Enum.TryParse<CtMode>(mode, out var m) ? m : CtMode.Server;

        return retVal;
    }

    public static int GetChunkSize(this string[] args)
    {
        var chunkSize = args.FirstOrDefault(arg => arg.StartsWith("--chunk-size="))?.Split('=')[1];
        var retVal = int.TryParse(chunkSize, out var cz) ? cz : 1024 * 1024 * 10;

        return retVal;
    }

    public static int GetThreadsCount(this string[] args)
    {
        var threads = args.FirstOrDefault(arg => arg.StartsWith("--threads="))?.Split('=')[1];
        var retVal = int.TryParse(threads, out var t) ? t : 2;

        return retVal;
    }

    public static string GetChunkMapPath(this string[] args)
    {
        var chunkMap = args.FirstOrDefault(arg => arg.StartsWith("--chunk-map="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(chunkMap)
            ? chunkMap
            : Path.Combine(AppContext.BaseDirectory, "chunkMap.json");

        return retVal;
    }

    public static string GetServerUrl(this string[] args)
    {
        var serverUrl = args.FirstOrDefault(arg => arg.StartsWith("--server-url="))?.Split('=')[1];
        var retVal = !string.IsNullOrEmpty(serverUrl)
            ? serverUrl
            : throw new ArgumentException("Server url is not specified");

        return retVal;
    }
}