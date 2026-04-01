using IntuneMonitor.Config;
using Microsoft.Extensions.Logging;
using System.CommandLine.Parsing;

namespace IntuneMonitor.Commands;

/// <summary>
/// Shared CLI helpers extracted from Program.cs for reuse and testability.
/// </summary>
internal static class CliHelpers
{
    /// <summary>
    /// Creates a logger factory with the specified minimum log level.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(LogLevel minLevel)
    {
        return LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(minLevel)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
        });
    }

    /// <summary>
    /// Applies command-line option overrides to the application configuration.
    /// </summary>
    public static void ApplyGlobalOverrides(
        AppConfiguration config,
        ParseResult result,
        GlobalOptions options)
    {
        var tenantId = result.GetValueForOption(options.TenantId);
        var clientId = result.GetValueForOption(options.ClientId);
        var clientSecret = result.GetValueForOption(options.ClientSecret);
        var certPath = result.GetValueForOption(options.CertPath);
        var certPassword = result.GetValueForOption(options.CertPassword);
        var certThumbprint = result.GetValueForOption(options.CertThumbprint);
        var backupPath = result.GetValueForOption(options.BackupPath);

        if (!string.IsNullOrWhiteSpace(tenantId)) config.Authentication.TenantId = tenantId;
        if (!string.IsNullOrWhiteSpace(clientId)) config.Authentication.ClientId = clientId;
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            config.Authentication.Method = "ClientSecret";
            config.Authentication.ClientSecret = clientSecret;
        }
        if (!string.IsNullOrWhiteSpace(certPath))
        {
            config.Authentication.Method = "Certificate";
            config.Authentication.CertificatePath = certPath;
        }
        if (!string.IsNullOrWhiteSpace(certPassword)) config.Authentication.CertificatePassword = certPassword;
        if (!string.IsNullOrWhiteSpace(certThumbprint))
        {
            config.Authentication.Method = "Certificate";
            config.Authentication.CertificateThumbprint = certThumbprint;
        }
        if (!string.IsNullOrWhiteSpace(backupPath)) config.Backup.Path = backupPath;
    }
}
