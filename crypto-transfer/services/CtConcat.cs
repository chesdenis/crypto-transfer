using ct.console.common;
using ct.console.model;

namespace ct.console.services;

public class CtConcat()
{
    public async Task Concat(string[] args)
    {
        var chunkMap = await args.LoadChunkMap();
        var encryptionKey = CtArgsExtensions.GetEncryptionKey();

        // Ensure the target directory exists
        var fileList = chunkMap.Values.Select(s => s.FilePath).Distinct().ToList();
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputFolder);

        foreach (var file in fileList)
        {
            var orderedChunks = chunkMap.Values
                .Where(w => w.FilePath == file)
                .OrderBy(chunk => chunk.Index).ToList();

            var fileName = Path.GetFileName(file);
            var outputFilePath = Path.Combine(outputFolder, fileName);

            await using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);

            foreach (var chunk in orderedChunks)
            {
                if (!File.Exists(chunk.FilePath))
                {
                    Console.WriteLine($"Chunk not found: {chunk.FilePath}");
                    throw new FileNotFoundException($"Chunk not found: {chunk.FilePath}");
                }

                await using var chunkStream = File.OpenRead(chunk.FilePath);
                chunkStream.Seek(chunk.Offset, SeekOrigin.Begin);

                var buffer = new byte[chunk.Length];
                await chunkStream.ReadExactlyAsync(buffer, 0, buffer.Length);

                var decryptedBytes = await buffer.DecryptAsync(encryptionKey);

                await outputFileStream.WriteAsync(decryptedBytes);

                Console.WriteLine($"Chunk {chunk.Index} of {chunk.Total} written to {outputFilePath}");
            }

            Console.WriteLine($"File assembled successfully into: {outputFilePath}");
        }
    }
}