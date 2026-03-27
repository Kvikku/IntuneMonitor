<div align="center">

# IntuneMonitor

**Back up, restore, and drift-detect your Microsoft Intune environment — from the terminal.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)]()
[![Microsoft Graph](https://img.shields.io/badge/Microsoft%20Graph-beta-0078D4?logo=microsoft&logoColor=white)](https://learn.microsoft.com/en-us/graph/overview)
[![xUnit](https://img.shields.io/badge/tests-xUnit-green?logo=testinglibrary&logoColor=white)]()

| Feature | Details |
|---|---|
| **Authentication** | Client secret **or** X.509 certificate (file or cert store thumbprint) |
| **Export** | Downloads all supported Intune content types to JSON backup files |
| **Import** | Restores policies from a backup into a target tenant |
| **Monitor** | Compares current live state with the backup and reports additions, removals, and field-level modifications |
| **HTML Reports** | Self-contained HTML dashboards for export summaries and change reports |
| **Storage backends** | Local file system **or** Git repository (auto-commit + push) |
| **Scheduling** | Run once (`--interval 0`) or loop on a configurable interval |
| **Logging** | Structured logging via `Microsoft.Extensions.Logging` with configurable verbosity (`--verbosity`) |
| **Configuration** | `appsettings.json`, environment variables, and/or CLI flags |
---

**Export** 13 Intune policy types to JSON · **Import** them into any tenant · **Monitor** for configuration drift  
Works interactively, in CI/CD pipelines, Azure Automation runbooks, or as a scheduled task.

</div>

---

## Highlights

- **One CLI, three commands** — `export`, `import`, `monitor` — plus `list-types` for discovery
- **13 policy types** — Settings Catalog, Compliance, Device Config, Scripts, Autopilot, Driver/Feature/Quality Updates, Assignment Filters, and more
- **Git-native backups** — every export becomes a versioned commit, auto-pushed to your remote
- **Deep diff engine** — field-level change detection with severity levels (Info / Warning / Critical)
- **HTML dashboards** — self-contained export summaries and change reports, auto-opened in the browser
- **Flexible auth** — client secret *or* X.509 certificate (PFX file, PEM, or Windows cert-store thumbprint)
- **Zero-config scheduling** — built-in polling loop or run once for external schedulers
- **Config anywhere** — `appsettings.json`, environment variables (`INTUNEMONITOR_` prefix), or CLI flags

---

## Quick Start

### 1. Clone & build

```bash
git clone https://github.com/Kvikku/IntuneMonitor.git
cd IntuneMonitor/src/IntuneMonitor
dotnet build
```

### 2. Configure

```bash
cp appsettings.example.json appsettings.json
```

Fill in your Entra ID app registration details:

```jsonc
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "ClientSecret",          // or "Certificate"
    "ClientSecret": "your-secret-here"
  },
  "Backup": {
    "StorageType": "LocalFile",        // or "Git"
    "Path": "./intune-backup"
  }
}
```

> [!CAUTION]
> `appsettings.json` is in `.gitignore` — **never commit secrets**.  
> Use environment variables in CI/CD (see [Configuration](#configuration)).

### 3. Run

```bash
# Export all policies to local backup
dotnet run -- export

# Detect drift against your last backup
dotnet run -- monitor

# Preview an import (no changes written)
dotnet run -- import --dry-run

# Actually restore policies
dotnet run -- import
```

That's it. You're backing up Intune.

---

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Runtime for building and running the app |
| Entra ID App Registration | With the Graph permissions below |

### Required API Permissions (Application)

| Permission | Needed for |
|---|---|
| `DeviceManagementConfiguration.Read.All` | Export & Monitor |
| `DeviceManagementConfiguration.ReadWrite.All` | Import |
| `DeviceManagementApps.Read.All` | Export & Monitor |
| `DeviceManagementManagedDevices.Read.All` | Export & Monitor |
| `DeviceManagementServiceConfig.Read.All` | Export & Monitor |

---

## Commands

### `export`

Downloads all (or selected) Intune policy types to JSON backup files.

```bash
# Export everything
dotnet run -- export

# Export specific types only
dotnet run -- export --content-types SettingsCatalog DeviceCompliancePolicy

# Export with a custom HTML report path
dotnet run -- export --html-report ./my-export-report.html
```

### `import`

Restores policies from a backup into the target tenant.

```bash
# Dry run — see what would be created
dotnet run -- import --dry-run

# Apply
dotnet run -- import
```

### `monitor`

Compares the live Intune state against your backup and reports changes.

```bash
# One-shot comparison
dotnet run -- monitor

# Continuous monitoring every 30 min, only print when things change
dotnet run -- monitor --interval 30 --changes-only

# Save a JSON report
dotnet run -- monitor --report-path ./drift-report.json

# Generate an HTML dashboard report
dotnet run -- monitor --html-report ./change-report.html
```

### `list-types`

Prints all 13 supported content types.

```bash
dotnet run -- list-types
```

---

## Supported Content Types

| Content Type | Graph Endpoint | Backup File |
|---|---|---|
| SettingsCatalog | `configurationPolicies` | `settingscatalog.json` |
| DeviceCompliancePolicy | `deviceCompliancePolicies` | `devicecompliance.json` |
| DeviceConfigurationPolicy | `deviceConfigurations` | `deviceconfiguration.json` |
| WindowsDriverUpdate | `windowsDriverUpdateProfiles` | `windowsdriverupdate.json` |
| WindowsFeatureUpdate | `windowsFeatureUpdateProfiles` | `windowsfeatureupdate.json` |
| WindowsQualityUpdateProfile | `windowsQualityUpdateProfiles` | `windowsqualityupdateprofile.json` |
| WindowsQualityUpdatePolicy | `windowsQualityUpdatePolicies` | `windowsqualityupdatepolicy.json` |
| PowerShellScript | `deviceManagementScripts` | `powershellscript.json` |
| ProactiveRemediation | `deviceHealthScripts` | `proactiveremediation.json` |
| MacOSShellScript | `deviceShellScripts` | `macosshellscript.json` |
| WindowsAutoPilotProfile | `windowsAutopilotDeploymentProfiles` | `windowsautopilot.json` |
| AppleBYODEnrollmentProfile | `appleUserInitiatedEnrollmentProfiles` | `applebyodenrollment.json` |
| AssignmentFilter | `assignmentFilters` | `assignmentfilter.json` |

---

## Configuration

All settings can come from three sources (highest priority wins):

```
CLI flags  >  Environment variables  >  appsettings.json
```

Environment variables use the `INTUNEMONITOR_` prefix with double-underscore separators:

```bash
export INTUNEMONITOR_Authentication__TenantId="..."
export INTUNEMONITOR_Authentication__ClientSecret="..."
export INTUNEMONITOR_Backup__Path="/mnt/backup"
```

<details>
<summary><b>Authentication settings</b></summary>

| Key | Description |
|---|---|
| `TenantId` | Entra tenant ID (GUID or domain) |
| `ClientId` | Application (client) ID |
| `Method` | `ClientSecret` (default) or `Certificate` |
| `ClientSecret` | Secret value (when Method = `ClientSecret`) |
| `CertificatePath` | Path to PFX or PEM file (when Method = `Certificate`) |
| `CertificatePassword` | Optional PFX password |
| `CertificateThumbprint` | Thumbprint for Windows cert-store lookup |

</details>

<details>
<summary><b>Backup settings</b></summary>

| Key | Description |
|---|---|
| `StorageType` | `LocalFile` (default) or `Git` |
| `Path` | Root directory for backups |
| `SubDirectory` | Optional sub-directory within `Path` |
| `GitRemoteUrl` | Remote URL (Git storage only) |
| `GitBranch` | Branch name (default: `main`) |
| `GitUsername` | HTTPS username |
| `GitToken` | Personal access token |
| `GitAuthorName` | Commit author name |
| `GitAuthorEmail` | Commit author email |
| `AutoCommit` | Auto-push after export (default: `true`) |
| `HtmlExportReportPath` | Path for the HTML export summary report (empty = skip) |
| `OpenHtmlExportReport` | Auto-open the HTML export report in the browser (default: `true`) |

</details>

<details>
<summary><b>Monitor settings</b></summary>

| Key | Description |
|---|---|
| `IntervalMinutes` | Polling interval. `0` = run once and exit |
| `ChangesOnly` | Only print output when changes exist |
| `ReportOutputPath` | Write JSON change report to this path |
| `HtmlReportOutputPath` | Path for the HTML change-report dashboard (empty = skip) |
| `OpenHtmlReport` | Auto-open the HTML change report in the browser (default: `true`) |
| `MinSeverity` | Minimum severity: `Info`, `Warning`, or `Critical` |

</details>

---

## Authentication

### Client Secret

```json
{
  "Authentication": {
    "Method": "ClientSecret",
    "ClientSecret": "your-secret"
  }
}
```

### Certificate (PFX file)

```json
{
  "Authentication": {
    "Method": "Certificate",
    "CertificatePath": "/path/to/cert.pfx",
    "CertificatePassword": "optional-password"
  }
}
```

### Certificate (Windows cert store)

```json
{
  "Authentication": {
    "Method": "Certificate",
    "CertificateThumbprint": "AABBCCDDEEFF..."
  }
}
```

---

## Git Storage Backend

Turn every export into a versioned Git commit — perfect for audit trails and rollbacks.

```json
{
  "Backup": {
    "StorageType": "Git",
    "Path": "./intune-backup-repo",
    "GitRemoteUrl": "https://github.com/your-org/intune-backup.git",
    "GitBranch": "main",
    "GitUsername": "git-user",
    "GitToken": "ghp_...",
    "AutoCommit": true
  }
}
```

If the target directory doesn't exist, IntuneMonitor will automatically:
1. `git init` a new repo
2. Add the remote
3. Create the branch
4. Commit and push on each export

---

## Scheduling

| Approach | How |
|---|---|
| **External scheduler** | Set `IntervalMinutes = 0` (default). Schedule via cron, Task Scheduler, Azure Automation, or GitHub Actions. |
| **Built-in loop** | Set `IntervalMinutes > 0`. The process stays alive and polls on that interval. |

```bash
# Poll every hour, suppress output when nothing changed
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

## Running Tests

```bash
dotnet test
```

---

## Project Structure

```
src/IntuneMonitor/
├── Authentication/     CredentialFactory (secret + certificate)
├── Commands/           Export, Import, Monitor commands
├── Comparison/         PolicyComparer — deep diff engine
├── Config/             Strongly-typed configuration POCOs
├── Graph/              IntuneExporter & IntuneImporter (Graph API)
├── Models/             IntuneItem, BackupDocument, ChangeReport
├── Reporting/          HtmlReportGenerator, HtmlExportReportGenerator, HtmlTheme
├── Storage/            IBackupStorage → LocalFileStorage, GitStorage
└── Program.cs          CLI entry point (System.CommandLine)

tests/IntuneMonitor.Tests/
└── PolicyComparerTests.cs
```

---

## Contributing

Contributions are welcome! Feel free to open issues and pull requests.

---

## License

This project is licensed under the [MIT License](LICENSE).
