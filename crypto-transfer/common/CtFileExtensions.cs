using System.Text.Json;

namespace ct.console.common;

public static class CtFileExtensions
{
    public static void Dump<T>(this T data, string outputPath)
    {
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"File {outputPath} was saved");
    }
}