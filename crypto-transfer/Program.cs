// See https://aka.ms/new-console-template for more information

using ct.console;
using ct.console.common;
using ct.console.model;
using ct.console.services;

Console.WriteLine("Hello, World!");

var mode = args.GetMode();

switch (mode)
{
    case CtMode.Server:
        // Generate the random key
        var encryptionKey = CtCryptoExtensions.GenerateRandomKey(32);
        encryptionKey.AsBase64String().Dump("enc.key");
        
        // run server mode with args
        var fileMapBuilder = new FileMapBuilder(new FileIterator());
        var extensionFilter = args.GetFileExtensionFilter();
        var chunkSize = args.GetChunkSize();
        var chunkMap = fileMapBuilder.Build(extensionFilter, chunkSize, encryptionKey);
        var server = new CtServer(chunkMap, encryptionKey.AsBase64String());
        server.Run(args);
        
        break;
    case CtMode.Downloader:
        var downloader = new CtDownloader();
        await downloader.Run(args);
        
        break;
    case CtMode.Combiner:
        break;
    default:
        throw new ArgumentOutOfRangeException();
}