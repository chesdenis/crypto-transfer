using ct.console.common;
using ct.console.model;

namespace ct.console.services;

public class CtServer(IDictionary<string, CtPart> chunkMap, string encryptionKey)
{
    public void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxConcurrentConnections = null;
            serverOptions.Limits.MaxConcurrentUpgradedConnections = null;
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
            serverOptions.Limits.MaxRequestBodySize = null;
            serverOptions.Limits.MinRequestBodyDataRate = null;
            serverOptions.Limits.MinResponseDataRate = null;
            serverOptions.Limits.MaxRequestHeadersTotalSize = 32768; // 32 KB (default is 16 KB)
            serverOptions.Limits.MaxRequestBufferSize = 2 * 1024 * 1024; // 2 MB
            serverOptions.Limits.MaxResponseBufferSize = 64 * 1024 * 1024; // 64 MB

            serverOptions.Limits.Http2.MaxStreamsPerConnection = 100; // Increase HTTP/2 streams per connection
            serverOptions.Limits.Http2.InitialStreamWindowSize = 6 * 1024 * 1024; // 6 MB stream window size
            serverOptions.Limits.Http2.InitialConnectionWindowSize = 12 * 1024 * 1024; // 12 MB connection window size
            serverOptions.AddServerHeader = false; // Disable 'Server' header for performance and security

            serverOptions.ListenAnyIP(8080);
        });
        
        var app = builder.Build();
        
        app.MapGet("/download/{partId}", async context =>
        {
            if (context.Request.RouteValues["partId"] is string partId 
                && chunkMap.TryGetValue(partId, out var partMeta) 
                && File.Exists(partMeta.FilePath))
            {
                await using var fileStream = File.OpenRead(partMeta.FilePath);
                fileStream.Seek(partMeta.Offset, SeekOrigin.Begin);

                var buffer = new byte[partMeta.Length];
                await fileStream.ReadExactlyAsync(buffer, 0, buffer.Length);

                var encryptedBytes = await buffer.Encrypt(encryptionKey);

                context.Response.ContentType = "application/octet-stream";
                context.Response.Headers["Content-Disposition"] = 
                    $"attachment; filename={partId}.part{partMeta.Index}_of_{partMeta.Total}.enc";
                context.Response.Headers["Content-Length"] = encryptedBytes.Length.ToString();

                await context.Response.Body.WriteAsync(encryptedBytes);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Chunk not found.");
            }
        });

        app.Run();
    }
}