# Improvements & Refactoring Opportunities

A prioritized list of improvements identified through a comprehensive codebase analysis. Organized by category with effort/impact ratings.

## Priority Matrix

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| Medium | 1.1 DI container | Medium | Testability |
| Medium | 3.1 Command tests | Medium | Reliability |
| Medium | 6.1 JSON summary for CI | Medium | CI robustness |
| Low | 4.1–4.3 Build config files | Small | Standardization |
| Low | 5.1–5.3 Report enhancements | Large | UX |
| Low | 1.4 Storage base class | Medium | Maintainability |

### Completed

- ~~1.2 Deduplicate retry logic~~ — `AuditLogFetcher` now delegates to `GraphRetryHandler`
- ~~1.3 Deduplicate JSON options~~ — `AzureBlobStorage` now uses `JsonDefaults`
- ~~2.1 Configurable retry params~~ — Added `GraphRetryConfig` to `AppConfiguration`
- ~~2.2 Centralize Graph URL~~ — All Graph URLs use `GraphClientFactory.GraphBetaBaseUrl` / `GraphV1BaseUrl`
- ~~4.4 Enable .NET analyzers~~ — `EnableNETAnalyzers` + `AnalysisLevel` set in both projects
- ~~7. Conditional Access & Named Locations~~ — Added to CI pipeline, `Policy.Read.ConditionalAccess` granted
- ~~2.3 Array comparison~~ — Order-insensitive array comparison in `FieldComparer`; reordering alone no longer produces false-positive diffs
- ~~1.5 Split CommandBuilder~~ — Split into `partial class` files per command (`CommandBuilder.Export.cs`, etc.)
- ~~3.2 Report tests~~ — 36 tests covering `HtmlReportGenerator`, `HtmlExportReportGenerator`, `HtmlAuditReportGenerator`, `MarkdownReportGenerator`, `MarkdownAuditReportGenerator`

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

- **Current**: `AzureBlobStorage` has `SanitizeBlobPath()` but `LocalFileStorage` relies only on `BackupFileHelpers.SanitizeFileName()`.
- **Change**: Add equivalent path validation.
- **Files**: `src/IntuneMonitor/Storage/LocalFileStorage.cs`

### 2.5 Consistent error propagation across commands

- **Current**: `ExportCommand` returns `0` on failure, `ImportCommand` accumulates errors in a list, `MonitorCommand` returns `EmptyReport()`.
- **Change**: Standardize error propagation pattern (e.g., a `CommandResult<T>` wrapper).

### 2.6 Audit Spectre.Console markup escaping

- **Current**: Mostly uses `Markup.Escape()` but pattern is inconsistent.
- **Change**: Audit all dynamic strings in UI code.
- **Files**: `src/IntuneMonitor/UI/ConsoleUI.cs`, `src/IntuneMonitor/UI/InteractiveMenu.cs`

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
