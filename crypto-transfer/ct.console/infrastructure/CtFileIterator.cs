namespace ct.console.infrastructure;

public class CtFileIterator
{
    public IEnumerable<string> WalkFilePath(string extensionFilter, string directoryToShare)
    {
        Console.WriteLine($"Searching for files with extension {extensionFilter} here {directoryToShare}");
        var files = Directory.GetFiles(directoryToShare, $"*.{extensionFilter}");
        var totalFiles = files.Length; 
        
        Console.WriteLine($"Found {totalFiles} files");
        
        var currentIndex = 0;

        foreach (var file in files)
        {
            // Increment the index
            currentIndex++;
            
            // Display progress
            Console.WriteLine($"Processing file {currentIndex}/{totalFiles} ({(currentIndex * 100) / totalFiles}%)");
            
            yield return file;
        }
    }
}