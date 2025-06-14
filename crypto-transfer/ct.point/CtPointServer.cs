using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ct.point;

public class CtPointServer
{
    public WebApplicationBuilder Create(string[] args)
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

        return builder;
    }

    public WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        app.MapGet("/ping", async context => { await context.Response.WriteAsync("pong"); });

        return app;
    }
}