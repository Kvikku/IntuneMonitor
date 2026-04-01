# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Diff command** — offline comparison of two backup snapshots without requiring a live tenant connection
- **Rollback command** — detect drift and restore policies to their backed-up state
- **Dependency command** — analyze policy relationships and dependencies across a backup
- **Validate command** — validate backup files for structural integrity
- **Notification/alerting system** — Teams, Slack, and Email notifications for drift detection via `INotificationSender` interface
- **Device Code Flow authentication** — interactive browser-based sign-in for environments where client secrets or certificates are not available
- **Multi-tenant TenantProfiles** — define named tenant profiles in configuration for easy switching between tenants
- **Azure Blob Storage backend** — store backups in Azure Blob Storage in addition to local file system and Git
- **Graph change notification subscriptions** — `GraphSubscriptionManager` for receiving real-time change notifications from Microsoft Graph
- 7 new Intune content types (20 total): Conditional Access Policies, App Protection Policies (managedAppPolicies), App Configuration Policies, Endpoint Security Policies, Enrollment Restrictions, Role Definitions, Named Locations
- **ContentTypeResolver** shared helper for unified content type lookups across commands
- **Shared helpers** — `JsonDefaults`, `GraphClientFactory`, `BackupFileHelpers`, `ReportWriter`, `HtmlReportHelpers` extracted to reduce duplication
- **Structured import errors** — `ImportResult` record with per-policy error categories (`ValidationError`, `AuthenticationError`, `NotFound`, `Conflict`, `Throttled`, `ServerError`) and descriptive messages
- **CSV report generator** — `CsvReportGenerator` for exporting change reports with formula injection protection

### Changed

- Refactored commands, Graph clients, storage, and reporting to use shared helper classes, eliminating code duplication
- Refactored `PolicyComparer` into four focused classes: `PolicyComparer` (orchestrator), `FieldComparer` (JSON diff), `AssignmentComparer` (assignment diff), `ChangeBuilder` (factory)
- Centralized Graph API retry logic in `GraphRetryHandler` (handles HTTP 429, 5xx, transient errors with exponential backoff)
- Extracted `WebhookNotificationSender` base class for Teams and Slack senders
- Consolidated HTML report generators via shared `HtmlReportHelpers`
- Refactored to use `IHttpClientFactory` and named HTTP clients via `GraphClientFactory`
- Slimmed `Program.cs` — moved command registration to `CommandBuilder`, global options to `GlobalOptions`, helpers to `CliHelpers`
- Extracted `MenuConstants` for all interactive menu text strings
- Improved `IntuneImporter` to return structured `ImportResult` instead of throwing exceptions
- Fixed double-encoding in HTML report page titles
- Guarded audit-log browser open behind `AuditLogConfig.OpenHtmlReport` configuration flag
- Used `subtitle2` parameter instead of inline HTML in export report generation

### Fixed

- **CSV formula injection** — sanitize exported CSV fields to prevent formula injection attacks
- **Path traversal protection** — validate blob paths in `AzureBlobStorage` to prevent directory traversal
- Pinned `System.Text.Json` to 8.0.5 to resolve GHSA-hh2w-p6rv-4g7w and GHSA-8g4q-xg66-9fp4
- Pinned `System.Text.RegularExpressions` to 4.3.1 to resolve GHSA-cmhx-cq75-c4mj
- Pinned `System.Net.Http` to 4.3.4 to resolve GHSA-7jgj-8wvc-jh57
- Fixed missing closing brace for `AuditLogConfig` class and missing opening summary tag for `NotificationConfig`

### CI/CD

- Added NuGet package caching to speed up builds
- Added code coverage collection and reporting
- Added `dotnet format` check to enforce code style
- Added vulnerability scanning with automatic CI failure on critical/high findings
- Added test reporter for richer test result display
- Added macOS ARM64 (`osx-arm64`) to cross-platform build targets
- Dropped `pull-requests:write` permission from vulnerability scan job
- Added `pipefail` to vulnerability scan script for correct exit codes

### Tests

- Added `ContentTypeResolver` tests
- Added formula injection sanitization test
- Added new tests for notification integration, diff, and rollback commands
- Added tests for `ImportResult`, `MenuConstants`, `CliHelpers`, `GraphClientFactory`
- Added Graph API integration tests with mock `HttpMessageHandler`
- Added `GraphRetryHandler` tests
- Added `BackupValidator` tests
- Added `NotificationFactory` tests
- Added `CsvReportGenerator` tests
- Added `CredentialFactory` tests

## [1.0.0] - 2026-03-27

### Added

- **Export command** — download Intune policies to JSON backup files with per-item storage
- **Import command** — restore policies from a backup into a target tenant (with `--dry-run` support)
- **Monitor command** — compare live state against backup and detect configuration drift with severity levels
- **Audit Log command** — fetch and summarize Intune audit log events (1–30 days) with throttling and retry logic
- **List Types command** — display all supported Intune content types
- **Interactive mode** — Spectre.Console menu-driven UI with arrow-key navigation when run with no arguments
- **HTML reports** — self-contained dashboards with dark/light theme toggle for export summaries, change reports, and audit logs
- **Storage backends** — local file system (timestamped run folders) or Git repository (auto-commit and push)
- **Authentication** — client secret or X.509 certificate (PFX/PEM file or Windows cert store thumbprint)
- **Configuration** — `appsettings.json`, environment variables (`INTUNEMONITOR_` prefix), and CLI flags (priority: CLI > env > file)
- **Structured logging** — `Microsoft.Extensions.Logging` with configurable verbosity via `--verbosity` flag
- **ConsoleUI** — Spectre.Console helpers (`WriteBanner`, `WriteHeader`, `StatusAsync`, `Info`, `Success`, `Warning`, `Error`)
- **Cross-platform** — self-contained builds for Windows x64, Linux x64, macOS x64
- **Settings catalog merging** — combines settings catalog policies with their full setting definitions
- **Assignment merging** — exports group assignments alongside policies with human-readable group name resolution
- **Timestamped reports** — `ReportPath` utility generates unique report file names to avoid overwriting
- 13 Intune policy types: Settings Catalog, Compliance Policies, Device Configuration Profiles, Group Policy (Administrative Templates), App Configuration Policies, Assignment Filters, Autopilot Deployment Profiles, Apple BYOD Enrollment Profiles, Device Enrollment Configurations, PowerShell Scripts, Shell Scripts, Driver Update Profiles, Feature Update Profiles
- Comprehensive documentation site (`docs/`) covering architecture, commands, configuration, authentication, storage, CI/CD, and troubleshooting
- `.github/copilot-instructions.md` with project conventions for AI coding agents
- 110 unit tests covering PolicyComparer, IntuneContentTypes, AppConfiguration, BackupModels, LocalFileStorage, BackupStorageFactory, MonitorCommand helpers, and IntuneImporter payload logic

### CI/CD

- GitHub Actions CI workflow — build, test, and format check on push and PR
- GitHub Actions Release workflow — version extraction, cross-platform publish, zip artifact creation, and GitHub release automation
- Separate test and publish jobs in release pipeline

### Fixed

- Fixed missing `successCount++` in `ImportCommand` non-dry-run path
- Removed redundant `ex.Message` from `LogError` calls across all commands
- Fixed null display in audit log HTML report
- Extracted backoff constant for audit log retry logic
- Applied URL encoding, response disposal, parse logging, and `--days` validator fixes in audit log command

### Changed

- Refactored file naming in `GitStorage` and `LocalFileStorage` to include short ID in JSON filenames
- Refactored `LocalFileStorage` to support timestamped export runs and improved backup management
- Routed `list-types` command output through structured logging instead of direct console writes
- Enhanced assignment change logging with human-readable diffs and virtual group name mappings
