// See https://aka.ms/new-console-template for more information

using ct.console;
using ct.console.common;
using ct.console.infrastructure;
using ct.console.model;
using ct.console.services;

Console.WriteLine("Hello, World!");

var mode = args.GetMode();

switch (mode)
{
    case CtMode.Server:
        // Generate the random key
        byte[] encryptionKey;
        if (!args.ReuseEncryptionKey())
        {
            encryptionKey = CtCryptoExtensions.GenerateRandomKey(32);
            encryptionKey.AsBase64String().Dump("enc.key");
            Console.WriteLine("Encryption key was generated and saved to project root.");
        }

        encryptionKey =  Convert.FromBase64String(args.GetEncryptionKey());
        
        var fileMapBuilder = new CtFileMapBuilder(new CtFileIterator());
        var extensionFilter = args.GetFileExtensionFilter();
        var chunkSize = args.GetChunkSize();
        var directoryToShare = args.GetDirectoryToShare();
        var chunkMap = fileMapBuilder.Build(extensionFilter, directoryToShare, chunkSize, encryptionKey);
        chunkMap.Dump(args.GetChunkMapPath());
        var server = new CtServer(chunkMap, encryptionKey.AsBase64String());
        server.Run(args);
        
        break;
    case CtMode.Downloader:
        var downloader = new CtDownloader();
        await downloader.Run(args);
        
        break;
    case CtMode.Combiner:
        var concat = new CtConcat();
        await concat.Concat(args);
        
        break;
    default:
        throw new ArgumentOutOfRangeException();
}