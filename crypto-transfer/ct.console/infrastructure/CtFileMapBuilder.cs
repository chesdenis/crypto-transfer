using ct.console.common;
using ct.console.model;

namespace ct.console.infrastructure;

public class CtFileMapBuilder(CtFileIterator fileIterator)
{
    public Dictionary<string, CtPart> Build(string extensionFilter, string directoryToShare, long chunkSize,
        byte[] encryptionKey)
    {
        var result = new Dictionary<string, CtPart>();

        foreach (var fp in fileIterator.WalkFilePath(extensionFilter, directoryToShare))
        {
            using var fileStream = File.OpenRead(fp);

            var totalChunks = (long)Math.Ceiling((double)fileStream.Length / chunkSize);
            var fileName = Path.GetFileName(fp);

            for (long i = 0; i < totalChunks; i++)
            {
                // important long here because due to multiplication we can overflow
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, fileStream.Length - offset);

                var chunkInfo = new { Index = i, Total = totalChunks, FileName = fileName };
                var chunkName = chunkInfo.Encrypt(encryptionKey);

                result[chunkName] = new CtPart(FilePath: fp, Offset: offset, Length: length, chunkInfo.Index,
                    chunkInfo.Total);
            }
        }

        return result;
    }
}