namespace IntuneMonitor.Config;

/// <summary>
/// Root application configuration loaded from appsettings.json and/or environment variables.
/// </summary>
public class AppConfiguration
{
    /// <summary>Authentication settings for connecting to Microsoft Entra ID.</summary>
    public AuthenticationConfig Authentication { get; set; } = new();

    /// <summary>Backup / export storage settings.</summary>
    public BackupConfig Backup { get; set; } = new();

    /// <summary>Monitoring and scheduling settings.</summary>
    public MonitorConfig Monitor { get; set; } = new();

    /// <summary>List of content types to process. Defaults to all when empty.</summary>
    public List<string> ContentTypes { get; set; } = new();
}

/// <summary>
/// Authentication configuration.
/// </summary>
public class AuthenticationConfig
{
    /// <summary>Microsoft Entra tenant ID (GUID or domain name).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) ID registered in Entra ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Authentication method: "ClientSecret" or "Certificate".
    /// </summary>
    public string Method { get; set; } = "ClientSecret";

    // --- Client Secret ---

    /// <summary>Client secret value (used when Method = "ClientSecret").</summary>
    public string? ClientSecret { get; set; }

    // --- Certificate ---

    /// <summary>
    /// Path to the PFX/PEM certificate file (used when Method = "Certificate").
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the PFX certificate (optional).
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Thumbprint of a certificate already installed in the local machine / current user cert store.
    /// Alternative to CertificatePath.
    /// </summary>
    public string? CertificateThumbprint { get; set; }
}

/// <summary>
/// Backup / export storage configuration.
/// </summary>
public class BackupConfig
{
    /// <summary>
    /// Storage backend type: "LocalFile" or "Git".
    /// </summary>
    public string StorageType { get; set; } = "LocalFile";

    /// <summary>Root directory where backup files are stored (LocalFile) or Git working directory.</summary>
    public string Path { get; set; } = "./intune-backup";

    /// <summary>Sub-directory within the backup root where JSON files are placed.</summary>
    public string SubDirectory { get; set; } = string.Empty;

    // --- Git-specific ---

    /// <summary>Remote Git URL (only for Git storage).</summary>
    public string? GitRemoteUrl { get; set; }

    /// <summary>Git branch to use. Defaults to "main".</summary>
    public string GitBranch { get; set; } = "main";

    /// <summary>Git username for authentication.</summary>
    public string? GitUsername { get; set; }

    /// <summary>Git personal access token (PAT) for authentication.</summary>
    public string? GitToken { get; set; }

    /// <summary>Commit author name for automated commits.</summary>
    public string GitAuthorName { get; set; } = "IntuneMonitor";

    /// <summary>Commit author email for automated commits.</summary>
    public string GitAuthorEmail { get; set; } = "intune-monitor@noreply.local";

    /// <summary>
    /// When true, automatically commit and push changes after an export.
    /// Requires GitRemoteUrl to be set.
    /// </summary>
    public bool AutoCommit { get; set; } = true;
}

/// <summary>
/// Monitoring / scheduling configuration.
/// </summary>
public class MonitorConfig
{
    /// <summary>
    /// Interval in minutes for scheduled monitoring runs.
    /// Set to 0 (or negative) to disable built-in scheduling (run once and exit).
    /// </summary>
    public int IntervalMinutes { get; set; } = 0;

    /// <summary>When true, only output changes to the console (suppress unchanged summary).</summary>
    public bool ChangesOnly { get; set; } = false;

    /// <summary>
    /// Path to a file where the change report is written as JSON.
    /// Leave empty to skip file output.
    /// </summary>
    public string? ReportOutputPath { get; set; }

    /// <summary>
    /// Minimum change severity to report: "Info", "Warning", or "Critical".
    /// Defaults to "Info" (report everything).
    /// </summary>
    public string MinSeverity { get; set; } = "Info";
}
