// See https://aka.ms/new-console-template for more information

using ct.client;
using ct.lib.extensions;
using ct.lib.logging;
using ct.lib.model;
using ct.lib.services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var encryptionKey = configuration.GetSection("Encryption:Key").Value;
if (string.IsNullOrWhiteSpace(encryptionKey))
    throw new InvalidOperationException("Encryption key is not configured properly.");

var leftLayout = new Layout();

var topRightLayout1 = new Layout();
var topRightLayout2 = new Layout();
var topRightLayout = new Layout("Items to share").SplitRows(topRightLayout1, topRightLayout2);

var bottomRightLayout = new Layout("Progress");

var rootLayout = new Layout()
    .SplitColumns(
        leftLayout,
        new Layout()
            .SplitRows(
                topRightLayout,
                bottomRightLayout));

AnsiConsole.Write(rootLayout);

var logQueue = new Queue<string>();


await AnsiConsole.Live(rootLayout).StartAsync(async ctx =>
{
    var loggerFactory = LoggerFactory.Create(builder => { builder.AddProvider(new CtLoggerProvider(ConsoleLog)); });

    var logger = loggerFactory.CreateLogger<Program>();
    
    var cryptoService = new CtCryptoService(loggerFactory.CreateLogger<CtCryptoService>());

    var client = new CtPointClient(args.GetServerUrl());
    var data = await client.InitiateAsync(new CtFile(args.GetTargetFile()));

    var keys = await Task.WhenAll(data.Keys.Select(async s =>
        await cryptoService.DecryptObjectAsync<CtPartKey>(s, encryptionKey)));

    foreach (var key in keys)
    {
        logger.LogInformation("Processing part {Index} of {Total} for file {FilePath}", key.Index, key.Total, key.FilePath);
    }

    return;
    
    void ConsoleLog(LogLevel logLevel, string category, string message)
    {
        var formattedMessage = CtLoggingExtensions.FormatLogMessage(logLevel, category, message);
        logQueue.Enqueue(formattedMessage);

        if (logQueue.Count > 10)
        {
            logQueue.Dequeue();
        }

        var combinedLogs = string.Join(Environment.NewLine, logQueue.ToArray());

        leftLayout.Update(
            new Panel(
                    Align.Left(
                        new Markup(combinedLogs),
                        VerticalAlignment.Top))
                .Expand()
        );

        ctx.Refresh();
    }
});