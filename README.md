<div align="center">

# IntuneMonitor

**Back up, restore, and drift-detect your Microsoft Intune environment — from the terminal.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![CI](https://github.com/Kvikku/IntuneMonitor/actions/workflows/ci.yml/badge.svg)](https://github.com/Kvikku/IntuneMonitor/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)]()
[![Microsoft Graph](https://img.shields.io/badge/Microsoft%20Graph-beta-0078D4?logo=microsoft&logoColor=white)](https://learn.microsoft.com/en-us/graph/overview)
[![xUnit](https://img.shields.io/badge/tests-xUnit-green?logo=testinglibrary&logoColor=white)]()

| Feature | Details |
|---|---|
| **Authentication** | Client secret, X.509 certificate (file or cert store), **or** Device Code Flow |
| **Export** | Downloads all supported Intune content types to JSON backup files |
| **Import** | Restores policies from a backup into a target tenant |
| **Monitor** | Compares current live state with the backup and reports additions, removals, and field-level modifications |
| **Rollback** | Detect drift and automatically revert policies to their backed-up state |
| **Diff** | Offline comparison of two backup snapshots without a live tenant connection |
| **Notifications** | Teams, Slack, and Email alerts for drift detection |
| **HTML Reports** | Self-contained HTML dashboards for export summaries, change reports, and audit logs |
| **Storage backends** | Local file system, Git repository (auto-commit + push), **or** Azure Blob Storage |
| **Scheduling** | Run once (`--interval 0`) or loop on a configurable interval |
| **Audit Logs** | Fetch and summarize Intune audit events with HTML/JSON reporting |
| **Logging** | Structured logging via `Microsoft.Extensions.Logging` with configurable verbosity (`--verbosity`) |
| **Configuration** | `appsettings.json`, environment variables, CLI flags, **or** named tenant profiles |
---

**Export** 20 Intune policy types to JSON · **Import** them into any tenant · **Monitor** for configuration drift  
Works interactively, in CI/CD pipelines, Azure Automation runbooks, or as a scheduled task.

</div>

---

## Highlights

- **Nine CLI commands** — `export`, `import`, `monitor`, `rollback`, `diff`, `dependency`, `validate`, `audit-log`, and `list-types`
- **Interactive mode** — run with no arguments for a menu-driven UI with arrow-key navigation
- **20 policy types** — Settings Catalog, Compliance, Device Config, Conditional Access, App Protection, Endpoint Security, Scripts, Autopilot, and more
- **Three storage backends** — local file system, Git (versioned commits, auto-pushed), or Azure Blob Storage
- **Deep diff engine** — field-level change detection with severity levels (Info / Warning / Critical)
- **HTML dashboards** — self-contained export summaries, change reports, and audit logs, auto-opened in the browser
- **Drift notifications** — Teams, Slack, or Email alerts when configuration drift is detected
- **Flexible auth** — client secret, X.509 certificate (PFX, PEM, cert-store thumbprint), or Device Code Flow
- **Zero-config scheduling** — built-in polling loop or run once for external schedulers
- **Multi-tenant profiles** — define named tenant profiles in configuration for easy switching
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

## Documentation

Full documentation is in the [`docs/`](docs/) folder:

| Guide | Description |
|---|---|
| [Getting Started](docs/getting-started.md) | Prerequisites, app registration, first export |
| [Commands](docs/commands.md) | Detailed reference for every CLI command and option |
| [Interactive Mode](docs/interactive-mode.md) | Menu-driven UI when running without arguments |
| [Configuration](docs/configuration.md) | All settings — appsettings.json, environment variables, CLI flags |
| [Authentication](docs/authentication.md) | Client secret, certificate file, or cert-store thumbprint |
| [Git Storage](docs/git-storage.md) | Version-controlled backups with auto-commit and push |
| [Monitoring & Scheduling](docs/monitoring.md) | Drift detection, scheduling, severity filtering |
| [CI/CD Integration](docs/cicd.md) | GitHub Actions, Azure DevOps, Docker examples |
| [Architecture](docs/architecture.md) | Project structure, design decisions, extensibility |
| [Troubleshooting](docs/troubleshooting.md) | Common issues and solutions |

---

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Runtime for building and running the app |
| Entra ID App Registration | With the Graph permissions listed in [Getting Started](docs/getting-started.md#required-api-permissions) |

---

## Commands

| Command | Description |
|---|---|
| `export` | Download all Intune policies to backup storage |
| `import` | Restore policies from backup into the tenant |
| `monitor` | Compare live state against backup, report drift |
| `rollback` | Detect drift and revert policies to backed-up state |
| `diff` | Compare two backup snapshots offline |
| `dependency` | Analyze policy relationships and dependencies |
| `validate` | Validate backup files for integrity |
| `audit-log` | Review Intune audit logs and summarize changes |
| `list-types` | Display all 20 supported content types |

```bash
dotnet run -- export                                  # Export all policies
dotnet run -- import --dry-run                        # Preview an import
dotnet run -- monitor --interval 30 --changes-only    # Continuous drift detection
dotnet run -- rollback --dry-run                      # Preview drift rollback
dotnet run -- diff --source ./backup-1 --target ./backup-2  # Compare two backups
dotnet run -- audit-log --days 7                      # Review last 7 days of audit logs
dotnet run -- list-types                              # Show supported types
dotnet run                                            # Interactive menu
```

See [Commands reference](docs/commands.md) for all options. Run without arguments for [interactive mode](docs/interactive-mode.md).

---

## Supported Content Types

| Content Type | Graph Endpoint | Backup Folder |
|---|---|---|
| SettingsCatalog | `configurationPolicies` | `SettingsCatalog/` |
| DeviceCompliancePolicy | `deviceCompliancePolicies` | `DeviceCompliancePolicy/` |
| DeviceConfigurationPolicy | `deviceConfigurations` | `DeviceConfigurationPolicy/` |
| WindowsDriverUpdate | `windowsDriverUpdateProfiles` | `WindowsDriverUpdate/` |
| WindowsFeatureUpdate | `windowsFeatureUpdateProfiles` | `WindowsFeatureUpdate/` |
| WindowsQualityUpdateProfile | `windowsQualityUpdateProfiles` | `WindowsQualityUpdateProfile/` |
| WindowsQualityUpdatePolicy | `windowsQualityUpdatePolicies` | `WindowsQualityUpdatePolicy/` |
| PowerShellScript | `deviceManagementScripts` | `PowerShellScript/` |
| ProactiveRemediation | `deviceHealthScripts` | `ProactiveRemediation/` |
| MacOSShellScript | `deviceShellScripts` | `MacOSShellScript/` |
| WindowsAutoPilotProfile | `windowsAutopilotDeploymentProfiles` | `WindowsAutoPilotProfile/` |
| AppleBYODEnrollmentProfile | `appleUserInitiatedEnrollmentProfiles` | `AppleBYODEnrollmentProfile/` |
| AssignmentFilter | `assignmentFilters` | `AssignmentFilter/` |
| ConditionalAccessPolicy | `conditionalAccess/policies` | `ConditionalAccessPolicy/` |
| AppProtectionPolicy | `managedAppPolicies` | `AppProtectionPolicy/` |
| AppConfigurationPolicy | `mobileAppConfigurations` | `AppConfigurationPolicy/` |
| EndpointSecurityPolicy | `intents` | `EndpointSecurityPolicy/` |
| EnrollmentRestriction | `deviceEnrollmentConfigurations` | `EnrollmentRestriction/` |
| RoleDefinition | `roleDefinitions` | `RoleDefinition/` |
| NamedLocation | `conditionalAccess/namedLocations` | `NamedLocation/` |

> **Note:** LocalFile and Git storage write one JSON file per policy item inside each folder (e.g., `SettingsCatalog/My_Policy_4a2b3c4d.json`). Azure Blob Storage uses a single blob per content type instead.

---

## Configuration

All settings come from three sources (highest priority wins):

```
CLI flags  >  Environment variables  >  appsettings.json
```

See [Configuration](docs/configuration.md) for the full reference, and [Authentication](docs/authentication.md) for credential setup.

---

## Git Storage

Turn every export into a versioned Git commit. See [Git Storage guide](docs/git-storage.md).

---

## Scheduling

| Approach | How |
|---|---|
| **Built-in loop** | `dotnet run -- monitor --interval 60` |
| **External scheduler** | Cron, Task Scheduler, GitHub Actions, Azure Automation |

See [Monitoring & Scheduling](docs/monitoring.md) and [CI/CD Integration](docs/cicd.md).

---

## Logging

Use `--verbosity` to control log output on any command:

```bash
dotnet run -- export --verbosity Debug    # Verbose
dotnet run -- monitor --verbosity Warning # Quiet
```

Levels: `Trace` → `Debug` → `Information` (default) → `Warning` → `Error` → `Critical` → `None`

---

## Running Tests

```bash
dotnet test
```

---

## Project Structure

```
src/IntuneMonitor/
├── Authentication/     CredentialFactory (secret, certificate, device code)
├── Commands/           Export, Import, Monitor, Rollback, Diff, Dependency, Validate, AuditLog
├── Comparison/         PolicyComparer, FieldComparer, AssignmentComparer, ChangeBuilder
├── Config/             Strongly-typed configuration POCOs
├── Graph/              IntuneExporter, IntuneImporter, AuditLogFetcher, GraphRetryHandler
├── Models/             IntuneItem, BackupDocument, ChangeReport, ImportResult, AuditModels
├── Notifications/      INotificationSender — Teams, Slack, Email
├── Reporting/          HTML, CSV, and JSON report generators
├── Storage/            IBackupStorage → LocalFileStorage, GitStorage, AzureBlobStorage
├── UI/                 ConsoleUI (Spectre.Console) + InteractiveMenu + MenuConstants
└── Program.cs          Entry point — CLI routing + interactive menu

tests/IntuneMonitor.Tests/
└── 25 test files (PolicyComparer, Graph clients, commands, storage, notifications, etc.)

docs/                   Full documentation
```

See [Architecture](docs/architecture.md) for design details.

---

## Contributing

Contributions are welcome! See the [Contributing guide](docs/contributing.md) for setup, conventions, and PR guidelines.

Coding conventions for the project (used by both humans and AI coding agents) are in [`.github/copilot-instructions.md`](.github/copilot-instructions.md).

---

## License

This project is licensed under the [MIT License](LICENSE).
