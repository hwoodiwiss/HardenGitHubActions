using HardenGitHubActions.Cli;
using HardenGitHubActions.Cli.Infrastructure;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var services = new ServiceCollection();

services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<Func<string?, LogLevel, WorkflowHardener>>(sp =>
    (token, level) =>
    {
        var console = sp.GetRequiredService<IAnsiConsole>();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(level);
            builder.AddProvider(new SpectreLoggerProvider(console, level));
        });
        var logger = loggerFactory.CreateLogger<WorkflowHardener>();
        return new WorkflowHardener(new GitHubApiClient(new HttpClient(), token), logger);
    });
services.AddSingleton<HardenCommand>();

var sp = services.BuildServiceProvider();

var rootCommand = HardenCommand.BuildRootCommand(sp);

await rootCommand.Parse(args).InvokeAsync();
