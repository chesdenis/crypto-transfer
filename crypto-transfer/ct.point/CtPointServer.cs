using System.Text.Json;
using ct.lib.extensions;
using ct.lib.model;
using ct.lib.services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ct.point;

public class CtPointServer
{
    public WebApplicationBuilder Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();
        
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

        builder.Services.AddSingleton<ICtCryptoService, CtCryptoService>();
        builder.Services.AddSingleton<ICtFileProviderService, CtFileProviderService>();

        return builder;
    }

    public WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();
        
        var config = app.Services.GetRequiredService<IConfiguration>();
        var encryptionKey = config.GetSection("Encryption:Key").Value;
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new InvalidOperationException("Encryption key is not configured properly.");
        
        app.MapGet("/ping", async context => { await context.Response.WriteAsync("pong"); });

        // building download map for file and initiate encryption key
        app.MapPost("/initiate", async context =>
        {
            var request = await context.Request.Body.As<CtFile>();
            var fileProvider = context.RequestServices.GetRequiredService<ICtFileProviderService>();
            var dictionary = await fileProvider.BuildMapAsync(request ?? throw new InvalidOperationException(), encryptionKey);
            
            await context.Response.WriteAsync(JsonSerializer.Serialize(dictionary));
        });

        app.MapPost("/download", async context =>
        {
            var request = await context.Request.Body.As<CtPartRequest>();

            request = request ?? throw new ArgumentNullException(nameof(request));

            await context.Response.WriteAsync(
                $"Download received for part: {request.FileKey}, {request.Start}, {request.End}");
        });

        app.MapPost("/check", async context =>
        {
            var request = await context.Request.Body.As<CtPartHashRequest>();

            request = request ?? throw new ArgumentNullException(nameof(request));

            await context.Response.WriteAsync(
                $"Hash request received for part: {request.FileKey}, {request.Start}, {request.End}");
        });

        return app;
    }
}