// See https://aka.ms/new-console-template for more information

using ct.lib.extensions;
using ct.point;
using Spectre.Console;


var leftLayout = new Layout("Left");
var topRightLayout = new Layout("TopRight");
var bottomRightLayout = new Layout("bottomRight");

// Ask for the user's favorite fruits
// Ask for the user's favorite fruits
var selectedFruits = AnsiConsole.Prompt(
    new MultiSelectionPrompt<string>()
        .Title("Select your favorite [green]fruits[/]:")
        .PageSize(10)
        .InstructionsText("[grey](Use [blue]<space>[/] to select, [green]<enter>[/] to accept)[/]")
        .AddChoiceGroup("Tropical Fruits", new[] { "Banana", "Coconut", "Pineapple" })
        .AddChoiceGroup("Berries", new[] { "Blueberry", "Blackcurrant", "Cloudberry" })
        .AddChoiceGroup("Exotic Fruits", new[] { "Durian", "Rambutan", "Dragon Fruit" }));

{
    var table = new Table();

    table.AddColumn("Foo");
    table.AddColumn(new TableColumn("Bar").Centered());

    foreach (var fruit in selectedFruits)
    {
        table.AddRow("Baz", "[green]Qux[/]");
        table.AddRow(new Markup("[blue]Corgi[/]"), new Panel("Waldo"));
    }
    
    topRightLayout.Update(table);
}


var rootLayout = new Layout("Root")
    .SplitColumns(
        leftLayout,
        new Layout("Right")
            .SplitRows(
                topRightLayout,
                bottomRightLayout));

AnsiConsole.Write(rootLayout);

var logQueue = new Queue<string>();

await AnsiConsole.Live(rootLayout).StartAsync(async ctx =>
{
    var server = new CtPointServer();
    var webAppBuilder = server.Create(args);
    webAppBuilder.Logging.ClearProviders()
        .AddProvider(new CtSpectreConsoleLoggerProvider(LogServerActivity));

    var webApp = server.Build(webAppBuilder);
    await webApp.RunAsync();
    return;

    void LogServerActivity(LogLevel logLevel, string category, string message)
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