namespace ct.console.services;

public class FileIterator
{
    public IEnumerable<string> WalkFilePath(string extensionFilter)
    {
        var currentDirectory = AppContext.BaseDirectory;
        var files = Directory.GetFiles(currentDirectory, $"*.{extensionFilter}");
        var totalFiles = files.Length; 
        
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