using System.Text.Json;
using ct.console.model;

namespace ct.console.common;

public static class CtFileExtensions
{
    public static void Dump<T>(this T data, string outputPath)
    {
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"File {outputPath} was saved");
    }

    public static async Task<Dictionary<string, CtPart>> LoadChunkMap(this string[] args)
    {
        var chunkMapFilePath = args.GetChunkMapPath();

        if (!File.Exists(chunkMapFilePath))
        {
            Console.WriteLine($"Chunk map file not found: {chunkMapFilePath}");
            throw new FileNotFoundException();
        }

        var chunkMapJson = await File.ReadAllTextAsync(chunkMapFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, CtPart>>(chunkMapJson) ??
               new Dictionary<string, CtPart>();
    }
}