# Commands

IntuneMonitor provides four commands, each accessible via `dotnet run -- <command>` or through the [interactive menu](interactive-mode.md).

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

Displays all 13 supported content types in a formatted table.

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
