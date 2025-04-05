using System.Collections.Concurrent;

namespace ct.console.common;

public static class CtProgressExtensions
{
    public static async Task ReportProgress(this ConcurrentDictionary<string, double> progressMap)
    {
        var completedChunks = progressMap.Values.Count(p => p >= 100); // Completed chunks
        var inProgressChunks = progressMap.Values.Count(p => p > 0 && p < 100); // In-progress chunks
        var totalChunks = progressMap.Count; // Total chunks
        var notStartedChunks = totalChunks - completedChunks - inProgressChunks; // Remaining chunks
        
        var bar = GenerateChunksProgressBar(
            completedChunks, 
            inProgressChunks, 
            notStartedChunks, totalChunks);

        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write($"Progress: {bar}");

        await Task.Delay(100);
    }

    private static string GenerateChunksProgressBar(int completedChunks, int inProgressChunks, int notStartedChunks, int totalChunks)
    {
        var completedPart = new string(':', completedChunks); // Completed chunks
        var inProgressPart = new string('.', inProgressChunks); // In-progress chunks
        var notStartedPart = new string(' ', notStartedChunks); // Not-started chunks

        var allParts = $"{completedPart}{inProgressPart}{notStartedPart}";

        var shuffledBar = allParts.ShuffleString();

        // Calculate percentage completion
        var percentageCompleted = totalChunks > 0 ? (completedChunks / (double)totalChunks) * 100 : 0;

        // Return progress bar with percentage
        return $"[{shuffledBar}] {percentageCompleted:F2}% completed";
    }


    private static string ShuffleString(this string input)
    {
        Random random = new Random();
        return new string(input.ToCharArray().OrderBy(c => random.Next()).ToArray());
    }
}