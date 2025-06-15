// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;
using ct.lib.extensions;
using ct.lib.logging;
using ct.lib.services;
using ct.point;
using Spectre.Console;

var directoryToShare = args.GetDirectoryToShare();
var extensionFilter = args.GetFileExtensionFilter();

// to display of what we going to share and transfer
var renderedFilesToShare = CtIoExtensions.GetFilesToShare(extensionFilter, directoryToShare)
    .OrderBy(o=>o).ToArray();
var renderedFoldersToShare = CtIoExtensions.GetFoldersToShare(extensionFilter, directoryToShare)
    .OrderBy(o=>o).ToArray();

var leftLayout = new Layout();

var topRightLayout1 = new Layout();
var topRightLayout2 = new Layout();
var topRightLayout = new Layout("Items to share").SplitRows(topRightLayout1, topRightLayout2);;

var bottomRightLayout = new Layout("Clients & Progress");

RenderTable(renderedFilesToShare, topRightLayout1, Path.GetFileName, item => new FileInfo(item).Length.ToHumanReadableSize());
RenderTable(renderedFoldersToShare, topRightLayout2, Path.GetFileName, item => "~");

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
    var server = new CtPointServer();
    var webAppBuilder = server.Create(args);
    webAppBuilder.Logging.ClearProviders()
        .AddProvider(new CtLoggerProvider(ConsoleLog));

    var webApp = server.Build(webAppBuilder);
    await webApp.RunAsync();
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

void RenderTable(string[] rows, Layout layout,
    Func<string, string?> renderName,
    Func<string, string> renderSize)
{
    var table = new Table();

    table.AddColumn("Name");
    table.AddColumn(new TableColumn("Size").Centered());

    int limit = 4;

    if (rows.Length == 0)
    {
        table.AddRow("-", "-");
    }

    foreach (var item in rows)
    {
        if (limit == 0)
        {
            table.AddRow("...", "...");
            table.AddRow($"Total: {rows.Length}", "");
            break;
        }
         
        table.AddRow(renderName(item) ?? string.Empty, renderSize(item));
        limit--;
    }
    
    layout.Update(table);
}