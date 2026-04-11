using HardenGitHubActions.Cli;
using HardenGitHubActions.Cli.Infrastructure;
using HardenGitHubActions.Core;
using HardenGitHubActions.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System.Reflection;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var services = new ServiceCollection();

services.AddSingleton<Func<string?, WorkflowHardener>>(
    token => new WorkflowHardener(new GitHubApiClient(new HttpClient(), token)));

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
