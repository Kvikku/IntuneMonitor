# Copilot Instructions for IntuneMonitor

## Project Overview

IntuneMonitor is a .NET 8 CLI tool for backing up, restoring, and drift-detecting Microsoft Intune configurations. It uses Azure.Identity for authentication, direct HttpClient calls to the Microsoft Graph beta API, System.CommandLine for CLI parsing, and Spectre.Console for rich terminal UI.

**User-facing docs live in `docs/`.** This file is for coding agents — covering conventions, patterns, and guardrails needed to make correct changes.

## Quick Reference

```bash
dotnet build                             # Build
dotnet test tests/IntuneMonitor.Tests/   # Run tests
dotnet run                               # Interactive menu (no args)
dotnet run -- export                     # Direct CLI mode (with args)
```

Target: **net8.0** · Tests: **xUnit 2.5.3** · UI: **Spectre.Console 0.49.1**

## Architecture

```
src/IntuneMonitor/
├── Authentication/   # CredentialFactory – client secret & certificate auth
├── Commands/         # ExportCommand, ImportCommand, MonitorCommand, AuditLogCommand
├── Comparison/       # PolicyComparer – deep JSON diff engine
├── Config/           # Strongly-typed configuration POCOs
├── Graph/            # IntuneExporter, IntuneImporter, AuditLogFetcher – Graph API clients
├── Models/           # IntuneItem, BackupDocument, ChangeReport, PolicyChange, AuditModels
├── Reporting/        # HtmlReportGenerator, HtmlExportReportGenerator, HtmlAuditReportGenerator, HtmlTheme, ReportPath
├── Storage/          # IBackupStorage + LocalFile/Git implementations
├── UI/               # ConsoleUI (Spectre.Console helpers) + InteractiveMenu
└── Program.cs        # Entry point – interactive menu (no args) or CLI routing (with args)

tests/IntuneMonitor.Tests/
└── PolicyComparerTests.cs

docs/                 # User-facing documentation (see docs/README.md for index)
```

> For deeper architecture details, see `docs/architecture.md`.

### Dual-Mode Entry Point

`Program.cs` checks `args.Length`:
- **No arguments** → `InteractiveMenu` (Spectre.Console prompts, arrow-key navigation)
- **With arguments** → `System.CommandLine` CLI routing

Both paths share the same `AppConfiguration`, command classes, and logger factory.

## Coding Conventions

### Naming

- **Classes**: PascalCase (`ExportCommand`, `PolicyComparer`, `LocalFileStorage`)
- **Async methods**: PascalCase with `Async` suffix (`RunAsync`, `ExportContentTypeAsync`)
- **Properties**: PascalCase auto-properties (`ContentType`, `PolicyData`)
- **Private fields**: `_camelCase` (`_logger`, `_config`, `_credential`)
- **Static readonly collections**: PascalCase (`WriteOptions`, `IgnoredFields`, `NoAssignmentsTypes`)

### Namespaces

Follow folder structure: `IntuneMonitor.{FolderName}` (e.g., `IntuneMonitor.Commands`, `IntuneMonitor.Graph`, `IntuneMonitor.UI`).

### File Organization

- Group closely related public types in the same file (e.g., configuration POCOs or backup models), rather than enforcing one public class per file.
- Use C# records for immutable data models (`IntuneItem`, `BackupDocument`, `PolicyChange`).
- Factory pattern for object creation (`CredentialFactory`, `BackupStorageFactory`).
- Static dictionaries in `IntuneContentTypes` for Graph endpoints, file names, and folder names.

### Nullability

Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Respect nullable annotations on all new code.

## Terminal UI (Spectre.Console)

Rich terminal output uses `Spectre.Console` via the `IntuneMonitor.UI` namespace:

- **`ConsoleUI`** — Static helpers: `WriteBanner()`, `WriteHeader()`, `WriteExportSummary()`, `WriteImportSummary()`, `WriteChangeReport()`, `WriteContentTypesTable()`, `StatusAsync()` (spinner), `Info()`, `Success()`, `Warning()`, `Error()`.
- **`InteractiveMenu`** — Menu-driven loop with `SelectionPrompt`, `MultiSelectionPrompt`, `TextPrompt`, `Tree` for settings view.

### Guidelines

- Use `ConsoleUI.StatusAsync("message", async () => ...)` for any operation that takes time (auth, Graph calls, storage writes).
- Use `Markup.Escape()` on all user-supplied or dynamic strings passed to Spectre markup.
- Keep `ConsoleUI` methods alongside (not instead of) `ILogger` calls — the logger goes to structured log output, `ConsoleUI` goes to the styled terminal.
- When adding new interactive workflows to `InteractiveMenu`, follow the existing pattern: prompt for options → run command → return to menu.

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
- `ExportCommand`/`ImportCommand` return an item count and use `0` items on failure; `MonitorCommand` returns a `ChangeReport`. CLI process exit codes are generally `0` unless `System.CommandLine` reports a parse error (command return values are not propagated to exit codes).
- All async methods accept and honor `CancellationToken`.

## Key Patterns

### Authentication

Two methods via `CredentialFactory`:

1. **Client secret** → `ClientSecretCredential`
2. **Certificate** (PFX/PEM file or Windows cert-store thumbprint) → `ClientCertificateCredential`

Uses `Azure.Identity` — **not** the Microsoft Graph SDK.

### Storage

`IBackupStorage` interface with two implementations:

- **LocalFileStorage**: Timestamp-based run folders, one JSON file per policy.
- **GitStorage**: Same folder layout inside a Git repo with auto-commit/push support.

Created via `BackupStorageFactory.Create()`.

### Configuration

Loaded in priority order (highest wins):

1. CLI flags
2. Environment variables (`INTUNEMONITOR_` prefix, `__` for nesting)
3. `appsettings.json`

Config POCOs live in `Config/AppConfiguration.cs`. Never commit `appsettings.json` (it's in `.gitignore`).

### Adding a New Content Type

1. Add a constant to `IntuneContentTypes`
2. Add entries to `GraphEndpoints`, `FileNames`, and `FolderNames` dictionaries
3. That's it — export, import, and monitor will pick it up automatically

### Graph API

All Graph calls go through `HttpClient` to the **beta** endpoint (`https://graph.microsoft.com/beta/...`). Endpoints and content-type mappings are centralized in `IntuneContentTypes`.

## CLI Commands

Built with `System.CommandLine 2.0.0-beta4.22272.1`. Five commands:

| Command | Purpose |
|---|---|
| `export` | Download Intune policies to backup storage |
| `import` | Restore policies from backup (supports `--dry-run`) |
| `monitor` | Compare live state vs. backup, detect drift, generate reports |
| `audit-log` | Fetch and summarize Intune audit log events (1–30 days) |
| `list-types` | Display the 13 supported Intune content types |

Global options (tenant, client, auth, backup path, verbosity) are defined in `Program.cs` and shared across commands.

## Testing

- Framework: **xUnit** with `Microsoft.NET.Test.SDK`.
- Test helpers: `MakeItem()`, `MakeBackup()` for constructing test data.
- Use raw JSON string literals for policy data in tests.
- Assertions: prefer `Assert.Single()`, `Assert.Contains()`, `Assert.Empty()`, `Assert.Equal()`.

## Dependencies

| Package | Purpose |
|---|---|
| `Azure.Identity` | Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | Config loading (JSON + env vars) |
| `Microsoft.Extensions.Logging.*` | Structured logging (console provider) |
| `Spectre.Console` | Rich terminal UI (tables, spinners, prompts, trees) |
| `System.CommandLine` | CLI parsing |

No Microsoft Graph SDK — raw `HttpClient` calls to the Graph beta API.

When adding new packages, prefer packages from the `Microsoft.Extensions.*` or `Azure.*` namespaces to stay consistent with the existing stack.

## Documentation Layout

The project has three documentation layers — each serves a different audience:

| Layer | Path | Audience | Purpose |
|---|---|---|---|
| **README** | `README.md` | First-time visitors | Quick start, feature overview, links to docs |
| **Docs** | `docs/` | Users & operators | Detailed guides for every feature, CI/CD, troubleshooting |
| **Copilot** | `.github/copilot-instructions.md` | AI coding agents | Conventions, patterns, guardrails for code changes |

When updating functionality:
- **Feature change** → Update the relevant `docs/*.md` page and this file if code patterns change
- **New command/option** → Update `docs/commands.md` and the commands table above
- **New dependency** → Update the dependencies table above and `docs/architecture.md`
- **README** should stay concise — link to docs for details, don't duplicate
