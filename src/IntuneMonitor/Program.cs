using IntuneMonitor.Commands;
using IntuneMonitor.Config;
using IntuneMonitor.Graph;
using IntuneMonitor.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.Net.Http.Headers;

// ---------------------------------------------------------------------------
// Banner
// ---------------------------------------------------------------------------
ConsoleUI.WriteBanner();

// ---------------------------------------------------------------------------
// Configuration loading
// ---------------------------------------------------------------------------
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "INTUNEMONITOR_");

var configuration = configBuilder.Build();
var appConfig = new AppConfiguration();
configuration.Bind(appConfig);

// ---------------------------------------------------------------------------
// IHttpClientFactory setup (proper connection pooling)
// ---------------------------------------------------------------------------
var services = new ServiceCollection();
services.AddHttpClient(GraphClientFactory.HttpClientName, client =>
{
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});
using var serviceProvider = services.BuildServiceProvider();
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

// ---------------------------------------------------------------------------
// Run – interactive menu when no args, CLI otherwise
// ---------------------------------------------------------------------------
if (args.Length == 0)
{
    var menu = new InteractiveMenu(appConfig, CliHelpers.CreateLoggerFactory, httpClientFactory);
    return await menu.RunAsync();
}

var (rootCommand, _) = CommandBuilder.Build(appConfig, httpClientFactory);
return await rootCommand.InvokeAsync(args);
