using System.Data.Common;
using ct.lib.model;
using Microsoft.Extensions.Logging;

namespace ct.lib.services;

public interface ICtFileProviderService
{
    Task<CtFileMap> BuildMapAsync(CtFile fi, string encryptionKey,
        long chunkSize = 100 * CtFileProviderService.Mb);
    
    Task<string> BuildPartAsync(CtPartRequest request, string encryptionKey);
}

public class CtFileProviderService(ICtCryptoService cryptoService) : ICtFileProviderService
{
    public const long Kb = 1024;
    public const long Mb = 1024 * Kb;
    public const long Gb = 1024 * Mb;

    public async Task<CtFileMap> BuildMapAsync(CtFile fi, string encryptionKey,
        long chunkSize = 100 * Mb)
    {
        var result = new Dictionary<string, string>();

        await using var fs = File.OpenRead(fi.FilePath);
        var fsLength = fs.Length;

        var totalParts = (long)Math.Ceiling((double)fsLength / chunkSize);
        for (long i = 0; i < totalParts; i++)
        {
            // important long here because due to multiplication we can overflow
            var offset = i * chunkSize;

            var length = Math.Min(chunkSize, fsLength - offset);
            var partKey = new CtPartKey(fi.FilePath, i, totalParts);
            var partValue = new CtPartValue(
                FilePath: fi.FilePath,
                Offset: offset,
                Length: length,
                partKey.Index,
                partKey.Total);

            var encryptedKey = await cryptoService.EncryptObjectAsync(partKey, encryptionKey);
            var encryptedValue = await cryptoService.EncryptObjectAsync(partValue, encryptionKey);
            result[encryptedKey] = encryptedValue;
        }

        return new CtFileMap(fi.FilePath, fsLength, result);
    }

    public async Task<string> BuildPartAsync(CtPartRequest request, string encryptionKey)
    {
        await using var fs = File.OpenRead(request.FilePath);

        var length = request.End - request.Start;
        fs.Seek(request.Start, SeekOrigin.Begin);
        
        var buffer = new byte[length];
        var bytesRead = await fs.ReadAsync(buffer.AsMemory(0, (int)length));

        if (bytesRead < length)
        {
            Array.Resize(ref buffer, bytesRead);
        }
        var encryptedData = await cryptoService.EncryptBytesAsync(buffer, encryptionKey);
        return Convert.ToBase64String(encryptedData);
    }
}