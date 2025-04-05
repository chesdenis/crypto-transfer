using ct.console.common;

namespace ct.console.services;

public class CtConcat
{
    public async Task Concat(string[] args)
    {
        var chunkMap = await args.LoadChunkMap();
        var encryptionKey = CtArgsExtensions.GetEncryptionKey();

        // Ensure the target directory exists
        var fileList = chunkMap.Values.Select(s => s.FilePath).Distinct().ToList();
        var outputFolder = args.GetOutput();
        Directory.CreateDirectory(outputFolder);
        Console.WriteLine($"Output folder: {outputFolder}");

        foreach (var file in fileList)
        {
            var orderedChunks = chunkMap.Values
                .Where(w => w.FilePath == file)
                .OrderBy(chunk => chunk.Index).ToList();

            var fileName = Path.GetFileName(file);
            var outputFilePath = Path.Combine(outputFolder, fileName);
            Console.WriteLine($"Output file: {outputFilePath}");

            await using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);

            foreach (var chunk in orderedChunks)
            {
                Console.WriteLine($"Writing chunk {chunk.Index} of {chunk.Total}...");
                
                if (!File.Exists(chunk.FilePath))
                {
                    Console.WriteLine($"Chunk not found: {chunk.FilePath}");
                    throw new FileNotFoundException($"Chunk not found: {chunk.FilePath}");
                }
                
                var partName =
                    $"{Path.GetFileName(chunk.FilePath)}.part{chunk.Index}_of_{chunk.Total}.enc";
                var partPath = Path.Combine(outputFolder, partName);
                
                await using var chunkStream = File.OpenRead(partPath);
                Console.WriteLine($"Chunk stream opened: {partName}");

                var buffer = new byte[chunkStream.Length];
                await chunkStream.ReadExactlyAsync(buffer, 0, buffer.Length);

                var decryptedBytes = await buffer.DecryptAsync(encryptionKey);

                await outputFileStream.WriteAsync(decryptedBytes);

                Console.WriteLine($"Chunk {chunk.Index} of {chunk.Total} written to {outputFilePath}");
            }

            Console.WriteLine($"File assembled successfully into: {outputFilePath}");
        }
    }
}