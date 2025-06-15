// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;
using ct.client;
using ct.lib.extensions;
using ct.lib.logging;
using ct.lib.model;
using ct.lib.services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
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

var logQueue = new ConcurrentQueue<string>();


await AnsiConsole.Live(rootLayout).StartAsync(async ctx =>
{
    var loggerFactory = LoggerFactory.Create(builder => { builder.AddProvider(new CtLoggerProvider(ConsoleLog)); });

    var logger = loggerFactory.CreateLogger<Program>();

    var cryptoService = new CtCryptoService(loggerFactory.CreateLogger<CtCryptoService>());

    var client = new CtPointClient(args.GetServerUrl(), cryptoService, encryptionKey);
    var data = await client.InitiateAsync(new CtFile(args.GetTargetFile()));

    var parts = new Dictionary<CtPartKey, CtPartValue>();

    foreach (var kvp in data.Parts)
    {
        var key = await cryptoService.DecryptObjectAsync<CtPartKey>(kvp.Key, encryptionKey);
        var value = await cryptoService.DecryptObjectAsync<CtPartValue>(kvp.Value, encryptionKey);
        parts.Add(key, value);
    }

    var fileLength = data.FileLength;
    var fileName = Path.GetFileName(data.FilePath);
    topRightLayout1.Update(new Text(fileName));
    topRightLayout2.Update(new Text($"File size: {fileLength.ToHumanReadableSize()}"));

    if (!Path.Exists(fileName))
    {
        CtIoExtensions.CreateBlankFile(fileName, fileLength);
    }


    await Parallel.ForEachAsync(parts, new ParallelOptions
        {
            MaxDegreeOfParallelism = 2
        },
        async (part, cancellationToken) =>
        {
            logger.LogInformation("Processing part {Index} of {Total} for file {FilePath}", part.Key.Index,
                part.Key.Total, part.Key.FilePath);

            var policy = Policy
                .Handle<Exception>() 
                .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    (exception, timeSpan, retryCount, context) =>
                    {
                        // Log each retry attempt
                        logger.LogWarning($"Attempt {retryCount} failed after {timeSpan}. Retrying...");
                        logger.LogWarning($"Exception: {exception.Message}");
                    });
            
            string partContent;
            byte[] partContentBytes = [];

            await policy.ExecuteAsync(async () =>
            {
                // Code block to retry
                var partRequest = new CtPartRequest(
                    part.Key.FilePath,
                    part.Value.Offset,
                    part.Value.Offset + part.Value.Length);

                partContent = await client.DownloadAsync(partRequest);
                partContentBytes = Convert.FromBase64String(partContent);
            });
            
            var decryptedPartContent = await cryptoService.DecryptAsync(partContentBytes, encryptionKey);
            lock (fileName) // To ensure thread-safe file writes
            {
                CtIoExtensions.WriteBytes(fileName, decryptedPartContent, part.Value.Offset);
            }

            var progress = Math.Round((part.Key.Index + 1) * 100.0 / part.Key.Total, 2);

            lock (bottomRightLayout) // To ensure thread-safe UI updates
            {
                bottomRightLayout.Update(new BarChart()
                    .Width(60)
                    .Label("[green bold underline]Download Progress[/]")
                    .LeftAlignLabel()
                    .AddItem($"Total", 100.0, Color.DarkCyan)
                    .AddItem($"{part.Key.Index + 1}/{part.Key.Total}", progress, Color.DarkGreen));
            }
        });

    return;

    void ConsoleLog(LogLevel logLevel, string category, string message)
    {
        var formattedMessage = CtLoggingExtensions.FormatLogMessage(logLevel, category, message);
        logQueue.Enqueue(formattedMessage);

        if (logQueue.Count > 10)
        {
            logQueue.TryDequeue(out _);
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

return;