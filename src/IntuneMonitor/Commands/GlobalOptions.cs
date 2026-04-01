using IntuneMonitor.Models;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace IntuneMonitor.Commands;

/// <summary>
/// Holds references to all global CLI options shared across commands.
/// </summary>
internal sealed class GlobalOptions
{
    public Option<string?> TenantId { get; }
    public Option<string?> ClientId { get; }
    public Option<string?> ClientSecret { get; }
    public Option<string?> CertPath { get; }
    public Option<string?> CertPassword { get; }
    public Option<string?> CertThumbprint { get; }
    public Option<string?> BackupPath { get; }
    public Option<string[]> ContentTypes { get; }
    public Option<LogLevel> Verbosity { get; }

    public GlobalOptions()
    {
        TenantId = new Option<string?>(
            "--tenant-id", "Microsoft Entra tenant ID (overrides appsettings.json)");
        ClientId = new Option<string?>(
            "--client-id", "Application client ID (overrides appsettings.json)");
        ClientSecret = new Option<string?>(
            "--client-secret", "Client secret (overrides appsettings.json)");
        CertPath = new Option<string?>(
            "--cert-path", "Path to PFX/PEM certificate file (overrides appsettings.json)");
        CertPassword = new Option<string?>(
            "--cert-password", "Certificate password (overrides appsettings.json)");
        CertThumbprint = new Option<string?>(
            "--cert-thumbprint", "Certificate thumbprint for cert-store lookup (overrides appsettings.json)");
        BackupPath = new Option<string?>(
            "--backup-path", "Path to backup storage directory (overrides appsettings.json)");
        ContentTypes = new Option<string[]>(
            "--content-types",
            () => Array.Empty<string>(),
            $"Content types to process. Available: {string.Join(", ", IntuneContentTypes.All)}")
        { AllowMultipleArgumentsPerToken = false };
        Verbosity = new Option<LogLevel>(
            "--verbosity",
            () => LogLevel.Information,
            "Set the logging verbosity level (Trace, Debug, Information, Warning, Error, Critical, None)");
    }

    /// <summary>
    /// Registers all global options on the specified root command.
    /// </summary>
    public void AddToCommand(RootCommand rootCommand)
    {
        rootCommand.AddGlobalOption(TenantId);
        rootCommand.AddGlobalOption(ClientId);
        rootCommand.AddGlobalOption(ClientSecret);
        rootCommand.AddGlobalOption(CertPath);
        rootCommand.AddGlobalOption(CertPassword);
        rootCommand.AddGlobalOption(CertThumbprint);
        rootCommand.AddGlobalOption(BackupPath);
        rootCommand.AddGlobalOption(ContentTypes);
        rootCommand.AddGlobalOption(Verbosity);
    }
}
