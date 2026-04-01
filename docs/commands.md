# Commands

IntuneMonitor provides nine commands, each accessible via `dotnet run -- <command>` or through the [interactive menu](interactive-mode.md).

## Global Options

These options are available on **all** commands and override values in `appsettings.json`:

| Option | Description |
|---|---|
| `--tenant-id <id>` | Microsoft Entra tenant ID |
| `--client-id <id>` | Application client ID |
| `--client-secret <secret>` | Client secret |
| `--cert-path <path>` | Path to PFX/PEM certificate file |
| `--cert-password <pass>` | Certificate password |
| `--cert-thumbprint <thumb>` | Certificate thumbprint (cert store) |
| `--backup-path <path>` | Backup storage directory |
| `--content-types <t1> <t2>` | Filter which content types to process |
| `--verbosity <level>` | Log level: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None` |

---

## `export`

Downloads all (or selected) Intune policy types to JSON backup files.

```bash
# Export everything
dotnet run -- export

# Export specific types only
dotnet run -- export --content-types SettingsCatalog DeviceCompliancePolicy

# Export with a custom HTML report path
dotnet run -- export --html-report ./my-export-report.html
```

### Options

| Option | Description |
|---|---|
| `--html-report <path>` | Path to write an HTML export summary report |

### Output

- JSON backup files written to the configured backup directory (one per content type)
- Optional HTML summary report with item counts and bar charts
- Console summary table showing items exported per content type

---

## `import`

Restores policies from a backup into the target tenant.

```bash
# Dry run — see what would be created
dotnet run -- import --dry-run

# Apply
dotnet run -- import
```

### Options

| Option | Description |
|---|---|
| `--dry-run` | Preview changes without creating anything in the tenant |

> [!TIP]
> Always run with `--dry-run` first to verify what will be imported.

---

## `monitor`

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

### Options

| Option | Description |
|---|---|
| `--report-path <path>` | Write a JSON change report |
| `--html-report <path>` | Write an HTML dashboard report |
| `--interval <minutes>` | Polling interval. `0` = run once |
| `--changes-only` | Only print output when changes are detected |

See [Monitoring & Scheduling](monitoring.md) for details on drift detection logic and severity levels.

---

## `list-types`

Displays all 20 supported content types in a formatted table.

```bash
dotnet run -- list-types
```

---

## `diff`

Compares two backup snapshots offline — no live tenant connection needed. Useful for reviewing what changed between two exports.

Both `--source` and `--target` should point to a **backup root directory** — the same path you use for `Backup.Path`. The command uses `LocalFileStorage` internally and loads the most recent timestamped run folder (e.g., `2024-01-15_143022/`) from each root.

```bash
# Compare two backup roots (each containing timestamped run folders)
dotnet run -- diff --source ./backup-prod --target ./backup-staging

# With HTML report output
dotnet run -- diff --source ./backup-old --target ./backup-new --html-report ./diff-report.html
```

### Options

| Option | Description |
|---|---|
| `--source <path>` | Path to the baseline backup root directory (required) |
| `--target <path>` | Path to the newer backup root directory (required) |
| `--html-report <path>` | Write an HTML diff report |
| `--json-report <path>` | Write a JSON diff report |

---

## `rollback`

Detects drift between the live tenant and the most recent backup, then restores policies to their backed-up state. Use `--dry-run` to preview what would be reverted.

```bash
# Preview what rollback would do
dotnet run -- rollback --dry-run

# Actually rollback drifted policies
dotnet run -- rollback
```

### Options

| Option | Description |
|---|---|
| `--dry-run` | Preview changes without modifying the tenant |

> [!TIP]
> Always run with `--dry-run` first to verify which policies would be reverted.

---

## `dependency`

Analyzes policy relationships and dependencies across your backup, helping you understand which policies reference or depend on others.

```bash
# Analyze dependencies
dotnet run -- dependency

# Export as JSON
dotnet run -- dependency --json-report ./dependencies.json
```

### Options

| Option | Description |
|---|---|
| `--json-report <path>` | Write a JSON dependency report |

---

## `validate`

Validates existing backup files for structural integrity, checking that stored content types are readable and JSON files are well-formed. It does not currently report missing content-type folders — only content types already present in storage are validated.

```bash
dotnet run -- validate
```

---

## `audit-log`

Fetches Intune audit log events from Microsoft Graph and summarizes changes over the last N days.

```bash
# Review last 7 days (default)
dotnet run -- audit-log

# Last 14 days with HTML report
dotnet run -- audit-log --days 14 --html-report ./audit-report.html

# Save a JSON report
dotnet run -- audit-log --days 7 --json-report ./audit-report.json
```

### Options

| Option | Description |
|---|---|
| `--days <n>` | Number of days to look back (1–30, default: 7) |
| `--html-report <path>` | Write an HTML audit log report |
| `--json-report <path>` | Write a JSON audit log report |

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
