using HardenGitHubActions.Cli;
using HardenGitHubActions.Cli.Infrastructure;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Reflection;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var services = new ServiceCollection();

services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
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

var registrar = new TypeRegistrar(services);
var app = new CommandApp<HardenCommand>(registrar);

app.Configure(config =>
{
    config.SetApplicationName("harden-actions");
    config.SetApplicationVersion(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0");
});

#pragma warning disable CA2007
return await app.RunAsync(args, cts.Token);
#pragma warning restore CA2007
