using System.Data.Common;
using ct.lib.model;
using Microsoft.Extensions.Logging;

namespace ct.lib.services;

public interface ICtFileProviderService
{
    Task<Dictionary<string, string>> BuildMapAsync(CtFile fi, string encryptionKey,
        long chunkSize = 50 * CtFileProviderService.Mb);
}

public class CtFileProviderService(ICtCryptoService cryptoService) : ICtFileProviderService
{
    public const long Kb = 1024;
    public const long Mb = 1024 * Kb;
    public const long Gb = 1024 * Mb;

    public async Task<Dictionary<string, string>> BuildMapAsync(CtFile fi, string encryptionKey,
        long chunkSize = 50 * Mb)
    {
        var result = new Dictionary<string, string>();
        var fileName = Path.GetFileName(fi.FilePath);

        await using var fs = File.OpenRead(fi.FilePath);
        var fsLength = fs.Length;

        var totalParts = (long)Math.Ceiling((double)fsLength / chunkSize);
        for (long i = 0; i < totalParts; i++)
        {
            // important long here because due to multiplication we can overflow
            var offset = i * chunkSize;

            var length = Math.Min(chunkSize, fsLength - offset);
            var partKey = new CtPartKey(fileName, i, totalParts);
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

        return result;
    }
}