# Improvements & Refactoring Opportunities

A prioritized list of improvements identified through comprehensive codebase analysis (last reviewed April 2026). Organized by category with effort/impact ratings.

## Priority Matrix

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| **High** | 2.7 Fix HttpClient socket exhaustion in AzureBlobStorage | Small | Reliability |
| **High** | 2.8 Fix HttpClient socket exhaustion in WebhookNotificationSender | Small | Reliability |
| **High** | 2.9 Triage 349 build warnings / enable TreatWarningsAsErrors | Medium | Code quality |
| Medium | 1.1 DI container | Medium | Testability |
| Medium | 2.4 Path traversal protection | Small | Security |
| Medium | 2.10 HTTPS validation on webhook URLs | Small | Security |
| Medium | 2.11 Deduplicate ResolveContentTypes in DiffCommand/RollbackCommand | Small | Maintainability |
| Medium | 2.12 Deduplicate JsonSerializerOptions in DiffCommand | Small | Maintainability |
| Medium | 3.1 Command tests | Medium | Reliability |
| Medium | 3.3 Notification sender tests | Small | Reliability |
| Medium | 3.5 Fix slow retry test (1m15s) | Small | DX |
| Medium | 6.1 JSON summary for CI | Medium | CI robustness |
| Medium | 8.1 Fix content-type count inconsistencies in docs | Small | Accuracy |
| Medium | 8.2 Add missing LICENSE file | Small | Compliance |
| Medium | 8.3 Add notifications documentation | Medium | Completeness |
| Low | 2.5 Consistent error propagation | Medium | Maintainability |
| Low | 2.13 InteractiveMenu mutates shared AppConfiguration | Small | Correctness |
| Low | 2.14 Records expose mutable List<> properties | Medium | Correctness |
| Low | 2.15 IntuneExporter group name cache has no TTL/size limit | Small | Long-running stability |
| Low | 2.16 ChangeReport computed properties re-enumerate on every access | Small | Performance |
| Low | 3.4 GitStorage/AzureBlobStorage tests | Medium | Reliability |
| Low | 4.1–4.3 Build config files | Small | Standardization |
| Low | 5.1–5.3 Report enhancements | Large | UX |
| Low | 1.4 Storage base class | Medium | Maintainability |
| Low | 8.4 Add Azure Blob Storage documentation | Medium | Completeness |
| Low | 8.5 Fix stale counts and references in docs | Small | Accuracy |
| Low | 8.6 Add missing config sections to docs | Small | Completeness |

### Completed

- ~~1.2 Deduplicate retry logic~~ — `AuditLogFetcher` now delegates to `GraphRetryHandler`
- ~~1.3 Deduplicate JSON options~~ — `AzureBlobStorage` now uses `JsonDefaults`
- ~~2.1 Configurable retry params~~ — Added `GraphRetryConfig` to `AppConfiguration`
- ~~2.2 Centralize Graph URL~~ — All Graph URLs use `GraphClientFactory.GraphBetaBaseUrl` / `GraphV1BaseUrl`
- ~~4.4 Enable .NET analyzers~~ — `EnableNETAnalyzers` + `AnalysisLevel` set in both projects
- ~~7. Conditional Access & Named Locations~~ — Added to CI pipeline, `Policy.Read.ConditionalAccess` granted
- ~~2.3 Array comparison~~ — Order-insensitive array comparison in `FieldComparer`; reordering alone no longer produces false-positive diffs
- ~~1.5 Split CommandBuilder~~ — Split into `partial class` files per command (`CommandBuilder.Export.cs`, etc.)
- ~~3.2 Report tests~~ — 41 tests covering `HtmlReportGenerator`, `HtmlExportReportGenerator`, `HtmlAuditReportGenerator`, `MarkdownReportGenerator`, `MarkdownAuditReportGenerator`, `CsvReportGenerator`
- ~~2.6 Audit Spectre.Console markup escaping~~ — `ConsoleUI` uses `Markup.Escape()` on all dynamic strings; `InteractiveMenu` uses a `SafeMarkup()` helper for all user-supplied values

---

## 1. Architecture & Dependency Injection

### 1.1 Introduce proper DI container usage

- **Current**: Only `IHttpClientFactory` is registered in the DI container. Commands manually `new` up `CredentialFactory`, `BackupStorageFactory`, `GraphClientFactory`, `IntuneExporter`, etc.
- **Impact**: Poor testability — can't mock dependencies in command-level tests.
- **Change**: Register key services (`IBackupStorage`, `INotificationService`, credential factory, Graph clients) in the DI container in `Program.cs`. Commands receive interfaces via constructor injection.
- **Files**: `src/IntuneMonitor/Program.cs`, all files in `Commands/`, `Graph/GraphClientFactory.cs`

### 1.4 Extract shared storage logic

- **Current**: `LocalFileStorage`, `GitStorage`, `AzureBlobStorage` duplicate JSON serialization, folder/file path lookup from `IntuneContentTypes` dictionaries.
- **Change**: Extract to a `BackupDocumentSerializer` or abstract base class.
- **Files**: All 3 storage implementations

---

## 2. Code Quality & Robustness

### 2.4 Add path traversal protection to LocalFileStorage

- **Current**: `AzureBlobStorage` has `SanitizeBlobPath()` that explicitly strips `..` segments, but `LocalFileStorage` relies only on `BackupFileHelpers.SanitizeFileName()` which replaces invalid filename characters and truncates — it does **not** guard against `..` path traversal. A malicious policy name containing `../` could escape the backup directory.
- **Change**: Add equivalent path validation that rejects or strips directory traversal sequences before constructing file paths.
- **Files**: `src/IntuneMonitor/Storage/LocalFileStorage.cs`, `src/IntuneMonitor/Storage/BackupFileHelpers.cs`

### 2.5 Consistent error propagation across commands

- **Current**: `ExportCommand` returns `0` on failure, `ImportCommand` accumulates errors in a list, `MonitorCommand` returns `EmptyReport()`.
- **Change**: Standardize error propagation pattern (e.g., a `CommandResult<T>` wrapper).

---

## 3. Testing

### 3.1 Add command-level integration tests

- **Current**: No tests for `ExportCommand`, `ImportCommand`, `MonitorCommand`.
- **Blocked by**: Manual dependency construction (see 1.1).
- **Change**: After DI refactor, test commands with mocked services.

### 3.3 Add notification sender tests

- **Current**: Factory tested but `TeamsWebhookSender`, `SlackWebhookSender`, `EmailNotificationSender` lack tests.
- **Change**: Test with `MockHttpHandler`.

### 3.4 Add GitStorage and AzureBlobStorage tests

- **Current**: Only `LocalFileStorage` has tests.
- **Change**: Test `GitStorage` with mocked `Process` calls; `AzureBlobStorage` with mock HTTP.

---

## 4. Build Tooling

### 4.1 Add `.editorconfig`

Enforce consistent formatting across IDEs (indentation, line endings, charset).

### 4.2 Add `Directory.Build.props`

Centralize shared properties (nullable, implicit usings, version info, authors) across both projects.

### 4.3 Add `global.json`

Lock SDK version to avoid build surprises across environments.

---

## 5. Reporting Enhancements

### 5.1 Side-by-side diff visualization in HTML reports

- **Current**: Field changes show old/new values in table cells, truncated at 300 chars.
- **Change**: Add collapsible side-by-side JSON diff with syntax highlighting.

### 5.2 Add search/filter/sort to HTML report tables

- **Current**: Static HTML, no interactivity beyond collapse.
- **Change**: Add lightweight JS for table sorting and filtering.

### 5.3 Add ARIA attributes for accessibility

- **Current**: Semantic HTML used but `aria-*` labels missing.
- **Change**: Add appropriate ARIA attributes to interactive elements and data tables.

---

## 6. CI/CD

### 6.1 Replace HTML regex parsing in notify stage

- **Current**: `.gitlab-ci.yml` notify stage parses HTML reports with regex to extract metrics for the Teams Adaptive Card.
- **Change**: Have the CLI output a machine-readable JSON summary alongside the report — CI can parse that instead of fragile regex on HTML.

---

## 7. New Findings (April 2026 Review)

### High Priority

### 2.7 Fix HttpClient socket exhaustion in AzureBlobStorage

- **Current**: `AzureBlobStorage.CreateHttpClientAsync()` creates `new HttpClient()` per call. Unlike the Graph layer (which correctly uses `IHttpClientFactory`), this risks socket exhaustion under sustained use.
- **Change**: Accept `IHttpClientFactory` via constructor and use it to create clients. Update `BackupStorageFactory.Create()` to pass the factory through.
- **Files**: `src/IntuneMonitor/Storage/AzureBlobStorage.cs`, `src/IntuneMonitor/Storage/BackupStorageFactory.cs`

### 2.8 Fix HttpClient socket exhaustion in WebhookNotificationSender

- **Current**: `WebhookNotificationSender` falls back to `new HttpClient()` when none is injected. `NotificationFactory` never passes one, so production always hits the fallback.
- **Change**: Pass `IHttpClientFactory` through `NotificationFactory` to all webhook senders. Register a named HTTP client (e.g., `"Notifications"`) in `Program.cs`.
- **Files**: `src/IntuneMonitor/Notifications/WebhookNotificationSender.cs`, `src/IntuneMonitor/Notifications/NotificationFactory.cs`, `src/IntuneMonitor/Program.cs`

### 2.9 Triage build warnings / enable TreatWarningsAsErrors

- **Current**: Build produces **349 warnings** with `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`. Most are:
  - **CA1848** — Use `LoggerMessage` delegates instead of `LoggerExtensions.Log*` for performance (~300+ warnings across all files with logging)
  - **CA1305** — Locale-dependent `StringBuilder.AppendLine` / `int.Parse` calls (in report generators and tests)
  - **CA1001** — Test classes owning disposable `MockHttpHandler` fields without implementing `IDisposable` (`ApplicationExportTests`, `AuditLogFetcherTests`, `IntuneImporterTests`, `IntuneExporterTests`)
  - **CA1816** — `Dispose()` implementations not calling `GC.SuppressFinalize()` (`CsvReportGeneratorTests`, `LocalFileStorageTests`)
- **Change**: Either suppress specific rules globally via `.editorconfig` (CA1848 is debatable for a CLI tool) or fix them. Then flip `TreatWarningsAsErrors` to `true` to prevent regression.
- **Files**: `src/IntuneMonitor/IntuneMonitor.csproj`, `tests/IntuneMonitor.Tests/IntuneMonitor.Tests.csproj`, all files with logging calls, test files with disposable fields

### Medium Priority

### 2.10 Add HTTPS validation on webhook URLs

- **Current**: `TeamsWebhookSender` and `SlackWebhookSender` use webhook URLs from config as-is without validating they use HTTPS. A misconfigured URL (or malicious config) could exfiltrate drift data over plaintext HTTP.
- **Change**: Validate that webhook URLs start with `https://` in `NotificationFactory.Create()` or in each sender's constructor. Log a warning or throw if HTTP is used.
- **Files**: `src/IntuneMonitor/Notifications/NotificationFactory.cs`, optionally `TeamsWebhookSender.cs`, `SlackWebhookSender.cs`

### 2.11 Deduplicate ResolveContentTypes in DiffCommand and RollbackCommand

- **Current**: Both `DiffCommand` and `RollbackCommand` have private `ResolveContentTypes()` methods that duplicate the logic already in `ContentTypeResolver.Resolve()`.
- **Change**: Replace the private methods with calls to the shared `ContentTypeResolver.Resolve()`.
- **Files**: `src/IntuneMonitor/Commands/DiffCommand.cs`, `src/IntuneMonitor/Commands/RollbackCommand.cs`

### 2.12 Deduplicate JsonSerializerOptions in DiffCommand

- **Current**: `DiffCommand` declares a private `static readonly JsonSerializerOptions ReportWriteOptions` that duplicates `JsonDefaults.IndentedCamelCase`.
- **Change**: Replace with `JsonDefaults.IndentedCamelCase`.
- **Files**: `src/IntuneMonitor/Commands/DiffCommand.cs`

### 3.5 Fix slow retry test

- **Current**: `IntuneImporterTests.ImportItemAsync_ServerError_ReturnsFailedResult` takes **1 minute 15 seconds** due to real retry backoff delays. `GraphRetryHandlerTests` also have tests taking 5–30 seconds each.
- **Change**: Configure shorter retry delays in test fixtures (e.g., inject a `GraphRetryConfig` with minimal backoff). Consider adding `CancellationToken` timeouts as a safety net.
- **Files**: `tests/IntuneMonitor.Tests/IntuneImporterTests.cs`, `tests/IntuneMonitor.Tests/GraphRetryHandlerTests.cs`

### Low Priority

### 2.13 InteractiveMenu mutates shared AppConfiguration singleton

- **Current**: `InteractiveMenu` mutates `_config` properties (e.g., `_config.Backup.HtmlExportReportPath`, `_config.Monitor.IntervalMinutes`) before running commands. Since `_config` is a shared singleton, mutations persist across menu loop iterations. If a user changes an option in one run, it silently affects subsequent runs in the same session.
- **Change**: Clone the relevant config section before mutation, or pass overrides as method parameters rather than mutating the shared object.
- **Files**: `src/IntuneMonitor/UI/InteractiveMenu.cs`

### 2.14 Records expose mutable List<> properties

- **Current**: `BackupDocument`, `ChangeReport`, `PolicyChange`, and other records use `List<T>` properties. Records imply immutability, but `List<>` is mutable — callers can `.Add()` / `.Remove()` items after construction.
- **Change**: Use `IReadOnlyList<T>` for public properties, or accept `List<T>` internally and expose via `IReadOnlyList<T>`.
- **Files**: `src/IntuneMonitor/Models/BackupModels.cs` (and any other record types with `List<>`)

### 2.15 IntuneExporter group name cache has no TTL or size limit

- **Current**: `IntuneExporter._groupNameCache` is a `Dictionary<string, string>` that grows unboundedly. In long-running scheduled monitoring scenarios (e.g., `--interval 60`), this accumulates stale group names indefinitely.
- **Change**: Clear the cache between export runs, or use a `ConcurrentDictionary` with a TTL wrapper. Alternatively, document that the cache is session-scoped and acceptable for typical use.
- **Files**: `src/IntuneMonitor/Graph/IntuneExporter.cs`

### 2.16 ChangeReport computed properties re-enumerate on every access

- **Current**: `ChangeReport.AddedCount`, `RemovedCount`, `ModifiedCount` are computed via `Changes.Count(...)` on every access. These are called multiple times in report generators, console output, and notification payloads.
- **Change**: Either cache the counts at construction time, or compute them lazily with backing fields.
- **Files**: `src/IntuneMonitor/Models/BackupModels.cs`

---

## 8. Documentation Fixes

### 8.1 Fix content-type count inconsistencies

- **Current**: The code has **21** content types in `IntuneContentTypes`, but multiple docs say "20":
  - `README.md` — tagline says "Export 20 Intune policy types"
  - `CHANGELOG.md` — `[Unreleased]` says "7 new Intune content types (20 total)"
  - `docs/gitlab-cicd.md` — references "20 Intune content types" in the export stage section
- **Change**: Update all references to "21".
- **Files**: `README.md`, `CHANGELOG.md`, `docs/gitlab-cicd.md`

### 8.2 Add missing LICENSE file

- **Current**: The README badge links to `LICENSE` and the footer says "licensed under the MIT License (LICENSE)" but no `LICENSE` file exists in the repo root.
- **Change**: Create a standard MIT `LICENSE` file in the repo root.
- **Files**: `LICENSE` (new)

### 8.3 Add notifications documentation

- **Current**: Teams, Slack, and Email notifications are a headline feature but have no dedicated guide. `docs/monitoring.md` doesn't even mention that notifications exist. Configuration tables in `docs/configuration.md` are the only reference.
- **Change**: Create `docs/notifications.md` covering:
  - How to set up Teams incoming webhooks
  - How to set up Slack incoming webhooks
  - SMTP email configuration
  - Message format and examples for each channel
  - Troubleshooting (webhook failures, SMTP errors, authentication)
- Also add a cross-reference from `docs/monitoring.md` to the new notifications page.
- **Files**: `docs/notifications.md` (new), `docs/monitoring.md`, `docs/README.md`

### 8.4 Add Azure Blob Storage documentation

- **Current**: There's an excellent `docs/git-storage.md` but no equivalent for Azure Blob Storage — a feature added in the Unreleased version.
- **Change**: Create `docs/azure-blob-storage.md` covering:
  - Azure Storage account setup
  - Authentication options (DefaultAzureCredential vs SAS token)
  - Container configuration
  - Configuration example
  - Comparison with local file and Git storage
- **Files**: `docs/azure-blob-storage.md` (new), `docs/README.md`

### 8.5 Fix stale counts and references in docs

- **Current**: Several stale numbers and missing references across docs:
  - `docs/architecture.md` says "25 test files" — actual count is **31**
  - `docs/architecture.md` is missing `ContentTypeResolver` from the Models listing
  - `docs/architecture.md` is missing `MarkdownReportGenerator` and `MarkdownAuditReportGenerator` from the Reporting listing
  - `README.md` project structure omits Markdown report generators from the Reporting description
  - `README.md` content-types table shows simplified Graph endpoints (e.g., `configurationPolicies`) — the actual code uses full paths (e.g., `deviceManagement/configurationPolicies`)
  - `CHANGELOG.md` lists `AppConfigurationPolicy` as both a v1.0.0 original and a new Unreleased type (duplicate)
  - `CHANGELOG.md` is missing comparison link at the bottom per Keep a Changelog format
  - `docs/getting-started.md` is missing `AuditLog.Read.All` from the API permissions table (present in `docs/gitlab-cicd.md`)
  - `docs/cicd.md` has no cross-link to `docs/gitlab-cicd.md`
  - GitHub CI badge in `README.md` will 404 if the repo is hosted on GitLab
- **Change**: Fix all stale references to match current codebase state.
- **Files**: `docs/architecture.md`, `README.md`, `CHANGELOG.md`, `docs/getting-started.md`, `docs/cicd.md`

### 8.6 Add missing config sections to docs

- **Current**: Two configurable sections exist in code but are not documented:
  - `AuditLogConfig` — controls `OpenHtmlReport`, `Days`, report output paths
  - `GraphRetryConfig` — controls retry count, backoff delay, max delay
- These were added in the Unreleased version but `docs/configuration.md` and `appsettings.example.json` don't include them.
- **Change**: Add `AuditLog` and `GraphRetry` sections to `docs/configuration.md` and `appsettings.example.json`.
- **Files**: `docs/configuration.md`, `src/IntuneMonitor/appsettings.example.json`

### 8.7 Add troubleshooting entries for new features

- **Current**: `docs/troubleshooting.md` covers auth, export, monitor, import, and interactive menu but has no entries for:
  - Notification failures (webhook 4xx/5xx, SMTP auth errors, DNS resolution)
  - Azure Blob Storage issues (connection strings, SAS token expiry, container permissions)
  - Diff/Rollback/Dependency/Validate command errors
- **Change**: Add troubleshooting sections for these features.
- **Files**: `docs/troubleshooting.md`

---

## 9. Security Observations (Low Risk — Documented for Awareness)

These are not bugs but worth noting for future hardening:

### 9.1 Client secret exposed via `--client-secret` CLI flag

- **Current**: `GlobalOptions.cs` allows passing the client secret as a CLI argument. Secrets on the command line are visible in process listings (`ps aux`), shell history, and CI logs.
- **Mitigation**: Already documented in README to prefer environment variables. Consider adding a runtime warning when `--client-secret` is used, suggesting `INTUNEMONITOR_AUTHENTICATION__CLIENTSECRET` instead.
- **Files**: `src/IntuneMonitor/Commands/GlobalOptions.cs`

### 9.2 Git credentials in environment variables

- **Current**: `GitStorage` sets `GIT_PASSWORD` as an environment variable (visible to child processes and via `/proc`). This is standard git credential-helper practice.
- **Risk**: Low — follows git conventions.
- **Files**: `src/IntuneMonitor/Storage/GitStorage.cs`

### 9.3 GitStorage commit message escaping

- **Current**: `GitStorage.RunGitCommandAsync` uses `commitMessage.Replace("\"", "\\\"")` for shell escaping. The message currently originates from code-generated strings (not user input), so the risk is low. If this ever accepts user input, shell injection becomes possible.
- **Mitigation**: Consider using `ProcessStartInfo.ArgumentList` (which handles escaping automatically) instead of string concatenation for git arguments.
- **Files**: `src/IntuneMonitor/Storage/GitStorage.cs`
