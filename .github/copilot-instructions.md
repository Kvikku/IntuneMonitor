# Copilot Instructions for IntuneMonitor

## Project Overview

IntuneMonitor is a .NET 8 CLI tool for backing up, restoring, and drift-detecting Microsoft Intune configurations. It uses Azure.Identity for authentication, direct HttpClient calls to the Microsoft Graph beta API, and System.CommandLine for CLI parsing.

## Architecture

```
src/IntuneMonitor/
├── Authentication/   # CredentialFactory – client secret & certificate auth
├── Commands/         # ExportCommand, ImportCommand, MonitorCommand
├── Comparison/       # PolicyComparer – deep JSON diff engine
├── Config/           # Strongly-typed configuration POCOs
├── Graph/            # IntuneExporter, IntuneImporter – Graph API clients
├── Models/           # IntuneItem, BackupDocument, ChangeReport, PolicyChange
├── Reporting/        # HtmlReportGenerator – self-contained HTML dashboards
├── Storage/          # IBackupStorage + LocalFile/Git implementations
└── Program.cs        # CLI entry point (System.CommandLine)

tests/IntuneMonitor.Tests/
└── PolicyComparerTests.cs   # xUnit tests for the diff engine
```

## Build & Test

```bash
dotnet build
dotnet test tests/IntuneMonitor.Tests/
```

The project targets **net8.0**. Tests use **xUnit 2.5.3**.

## Coding Conventions

### Naming

- **Classes**: PascalCase (`ExportCommand`, `PolicyComparer`, `LocalFileStorage`)
- **Async methods**: PascalCase with `Async` suffix (`RunAsync`, `ExportContentTypeAsync`)
- **Properties**: PascalCase auto-properties (`ContentType`, `PolicyData`)
- **Private fields**: `_camelCase` (`_logger`, `_config`, `_credential`)
- **Static readonly collections**: `UPPER_SNAKE_CASE`

### Namespaces

Follow folder structure: `IntuneMonitor.{FolderName}` (e.g., `IntuneMonitor.Commands`, `IntuneMonitor.Graph`).

### File Organization

- One public class per file.
- Use C# records for immutable data models (`IntuneItem`, `BackupDocument`, `PolicyChange`).
- Factory pattern for object creation (`CredentialFactory`, `BackupStorageFactory`).
- Static dictionaries in `IntuneContentTypes` for Graph endpoints, file names, and folder names.

### Nullability

Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Respect nullable annotations on all new code.

## Logging

Use `Microsoft.Extensions.Logging` with structured message templates:

```csharp
_logger.LogInformation("Export complete. {TotalItems} total item(s) exported", totalItems);
_logger.LogError(ex, "Failed to import '{ItemName}'", itemName);
```

- Pass the exception as the **first argument** to `LogError`.
- Use named placeholders (`{ContentType}`, `{ItemCount}`), not string interpolation.
- All commands accept an optional `ILoggerFactory` parameter, defaulting to `NullLoggerFactory.Instance`.
- Verbosity is controlled via the `--verbosity` CLI flag and `CreateLoggerFactory()` in `Program.cs`.

## Error Handling

- Wrap command execution in try-catch at the command level.
- Log errors with `LogError(ex, "descriptive message with {Context}")`.
- Current commands return `0` on failure (existing convention—note this suppresses non-zero exit codes).
- All async methods accept and honor `CancellationToken`.

## Authentication

Two methods via `CredentialFactory`:

1. **Client secret** → `ClientSecretCredential`
2. **Certificate** (PFX/PEM file or Windows cert-store thumbprint) → `ClientCertificateCredential`

Uses `Azure.Identity`—**not** the Microsoft Graph SDK.

## Storage

`IBackupStorage` interface with two implementations:

- **LocalFileStorage**: Timestamp-based run folders, one JSON file per policy.
- **GitStorage**: Same folder layout inside a Git repo with auto-commit/push support.

Created via `BackupStorageFactory.Create()`.

## Configuration

Loaded in priority order (highest wins):

1. CLI flags
2. Environment variables (`INTUNEMONITOR_` prefix, `__` for nesting)
3. `appsettings.json`

Config POCOs live in `Config/AppConfiguration.cs`. Never commit `appsettings.json` (it's in `.gitignore`).

## CLI Commands

Built with `System.CommandLine 2.0.0-beta4`. Four commands:

| Command | Purpose |
|---|---|
| `export` | Download Intune policies to backup storage |
| `import` | Restore policies from backup (supports `--dry-run`) |
| `monitor` | Compare live state vs. backup, detect drift, generate reports |
| `list-types` | Display the 13 supported Intune content types |

Global options (tenant, client, auth, backup path, verbosity) are defined in `Program.cs` and shared across commands.

## Testing

- Framework: **xUnit** with `Microsoft.NET.Test.SDK`.
- Test helpers: `MakeItem()`, `MakeBackup()` for constructing test data.
- Use raw JSON string literals for policy data in tests.
- Assertions: prefer `Assert.Single()`, `Assert.Contains()`, `Assert.Empty()`, `Assert.Equal()`.

## Dependencies

- `Azure.Identity` for Entra ID authentication
- `Microsoft.Extensions.Configuration` (JSON + environment variable providers)
- `Microsoft.Extensions.Logging` (console provider)
- `System.CommandLine` for CLI parsing
- No Microsoft Graph SDK—raw `HttpClient` calls to the Graph beta API

When adding new packages, prefer packages from the `Microsoft.Extensions.*` or `Azure.*` namespaces to stay consistent with the existing stack.

## Graph API

All Graph calls go through `HttpClient` to the **beta** endpoint (`https://graph.microsoft.com/beta/...`). Endpoints and content-type mappings are centralized in `IntuneContentTypes`.
