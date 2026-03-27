# IntuneMonitor

A cross-platform .NET 8 console application that **exports**, **imports**, and **monitors** Microsoft Intune configuration policies.  
It can run interactively, in CI/CD pipelines, or as an Azure Automation runbook / scheduled task.

## Features

| Feature | Details |
|---|---|
| **Authentication** | Client secret **or** X.509 certificate (file or cert store thumbprint) |
| **Export** | Downloads all supported Intune content types to JSON backup files |
| **Import** | Restores policies from a backup into a target tenant |
| **Monitor** | Compares current live state with the backup and reports additions, removals, and field-level modifications |
| **Storage backends** | Local file system **or** Git repository (auto-commit + push) |
| **Scheduling** | Run once (`--interval 0`) or loop on a configurable interval |
| **Logging** | Structured logging via `Microsoft.Extensions.Logging` with configurable verbosity (`--verbosity`) |
| **Configuration** | `appsettings.json`, environment variables, and/or CLI flags |

### Supported content types

| Content type | Backup file |
|---|---|
| SettingsCatalog | `settingscatalog.json` |
| DeviceCompliancePolicy | `devicecompliance.json` |
| DeviceConfigurationPolicy | `deviceconfiguration.json` |
| WindowsDriverUpdate | `windowsdriverupdate.json` |
| WindowsFeatureUpdate | `windowsfeatureupdate.json` |
| WindowsQualityUpdateProfile | `windowsqualityupdateprofile.json` |
| WindowsQualityUpdatePolicy | `windowsqualityupdatepolicy.json` |
| PowerShellScript | `powershellscript.json` |
| ProactiveRemediation | `proactiveremediation.json` |
| MacOSShellScript | `macosshellscript.json` |
| WindowsAutoPilotProfile | `windowsautopilot.json` |
| AppleBYODEnrollmentProfile | `applebyodenrollment.json` |
| AssignmentFilter | `assignmentfilter.json` |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An **Entra ID App Registration** with:
  - `DeviceManagementConfiguration.Read.All` (and `.ReadWrite.All` if importing)
  - `DeviceManagementApps.Read.All`
  - `DeviceManagementManagedDevices.Read.All`
  - `DeviceManagementServiceConfig.Read.All`
  - Client secret **or** a certificate uploaded to the app registration

---

## Quick start

### 1. Clone & build

```bash
git clone https://github.com/Kvikku/IntuneMonitor.git
cd IntuneMonitor/src/IntuneMonitor
dotnet build
```

### 2. Configure

Copy the example configuration and fill in your values:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json`:

```json
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "ClientSecret",
    "ClientSecret": "your-client-secret-here"
  },
  "Backup": {
    "StorageType": "LocalFile",
    "Path": "./intune-backup"
  },
  "Monitor": {
    "IntervalMinutes": 0
  }
}
```

> **Security note**: `appsettings.json` is listed in `.gitignore` and should never be committed.  
> Use environment variables (see below) in production / CI scenarios.

### 3. Run

```bash
# Export all Intune content to the backup directory
dotnet run -- export

# Compare live state against backup and print a change report
dotnet run -- monitor

# Import backup into the tenant (preview first with --dry-run)
dotnet run -- import --dry-run
dotnet run -- import
```

---

## Configuration reference

All settings can be provided via `appsettings.json` **or** environment variables prefixed with `INTUNEMONITOR_`.  
Environment variables use double-underscore (`__`) as the path separator.

Examples:
```bash
INTUNEMONITOR_Authentication__TenantId=...
INTUNEMONITOR_Authentication__ClientId=...
INTUNEMONITOR_Authentication__ClientSecret=...
INTUNEMONITOR_Backup__Path=/mnt/backup
```

CLI flags override both sources. See `dotnet run -- --help` for all flags.

### Authentication section

| Key | Description |
|---|---|
| `TenantId` | Entra tenant ID (GUID or domain) |
| `ClientId` | Application (client) ID |
| `Method` | `ClientSecret` (default) or `Certificate` |
| `ClientSecret` | Secret value (Method = ClientSecret) |
| `CertificatePath` | Path to a PFX or PEM file (Method = Certificate) |
| `CertificatePassword` | Optional PFX password |
| `CertificateThumbprint` | Thumbprint for cert-store lookup (alternative to file) |

### Backup section

| Key | Description |
|---|---|
| `StorageType` | `LocalFile` (default) or `Git` |
| `Path` | Root directory for backup files |
| `SubDirectory` | Optional sub-directory within `Path` |
| `GitRemoteUrl` | Git remote URL (Git storage only) |
| `GitBranch` | Branch name (default: `main`) |
| `GitUsername` | Git username for HTTPS auth |
| `GitToken` | Personal access token |
| `GitAuthorName` | Commit author name |
| `GitAuthorEmail` | Commit author email |
| `AutoCommit` | Push after each export (default: `true`) |

### Monitor section

| Key | Description |
|---|---|
| `IntervalMinutes` | Polling interval. `0` = run once and exit |
| `ChangesOnly` | Only print output when changes are found |
| `ReportOutputPath` | Write JSON change report to this file |
| `MinSeverity` | Minimum severity to print: `Info`, `Warning`, `Critical` |

---

## Certificate authentication

### Using a certificate file (PFX)

```json
"Authentication": {
  "Method": "Certificate",
  "CertificatePath": "/path/to/cert.pfx",
  "CertificatePassword": "optional-password"
}
```

### Using the Windows certificate store

```json
"Authentication": {
  "Method": "Certificate",
  "CertificateThumbprint": "AABBCCDDEEFF..."
}
```

---

## Git storage backend

Store backups in a Git repository so every export becomes a versioned commit:

```json
"Backup": {
  "StorageType": "Git",
  "Path": "./intune-backup-repo",
  "GitRemoteUrl": "https://github.com/your-org/intune-backup.git",
  "GitBranch": "main",
  "GitUsername": "git-user",
  "GitToken": "ghp_...",
  "AutoCommit": true
}
```

If the directory doesn't exist, IntuneMonitor will initialise a new Git repository and add the remote automatically.

---

## Scheduled monitoring (Azure Automation / cron)

### Run once (Azure Automation / serverless)

Set `Monitor.IntervalMinutes = 0` (default). The process exits after one run.  
Schedule the job externally (Azure Automation schedule, GitHub Actions `schedule`, cron, Task Scheduler).

### Built-in polling loop

Set `Monitor.IntervalMinutes = 60` to poll every hour. The process stays alive and loops.

```bash
dotnet run -- monitor --interval 60 --changes-only
```

---

## Logging

IntuneMonitor uses `Microsoft.Extensions.Logging` for structured logging output.

### Controlling log verbosity

Use the `--verbosity` global flag to control the log level for any command:

```bash
# Default – informational messages
dotnet run -- export

# Verbose / debug output (shows per-item Graph progress)
dotnet run -- export --verbosity Debug

# Quiet – only warnings and errors
dotnet run -- monitor --verbosity Warning

# Silent – suppress all log output
dotnet run -- export --verbosity None
```

Available levels (from most to least verbose):  
`Trace` → `Debug` → `Information` (default) → `Warning` → `Error` → `Critical` → `None`

---

## Running tests

```bash
dotnet test tests/IntuneMonitor.Tests/
```

---

## Project structure

```
src/
└── IntuneMonitor/
    ├── Authentication/     # CredentialFactory (client secret + certificate)
    ├── Commands/           # ExportCommand, ImportCommand, MonitorCommand
    ├── Comparison/         # PolicyComparer (change detection)
    ├── Config/             # AppConfiguration POCO
    ├── Graph/              # IntuneExporter, IntuneImporter (Graph API)
    ├── Models/             # IntuneItem, BackupDocument, ChangeReport, …
    ├── Storage/            # IBackupStorage, LocalFileStorage, GitStorage
    └── Program.cs          # CLI entry point (System.CommandLine)
tests/
└── IntuneMonitor.Tests/    # xUnit tests
```
