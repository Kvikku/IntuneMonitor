# Interactive Mode

When you launch IntuneMonitor **without any arguments**, it starts an interactive menu-driven UI:

```bash
dotnet run
```

## Main Menu

The application displays a selection prompt with arrow-key navigation:

```
What would you like to do?
> Export policies
  Import policies
  Monitor for changes
  Rollback drift
  Compare backups (diff)
  Analyze dependencies
  Validate backups
  Review audit logs
  List content types
  Settings overview
  Exit
```

Use **↑/↓** to navigate and **Enter** to select.

## Export Workflow

1. **Filter content types?** — Choose "Yes" to select specific types from a multi-select list, or "No" to export all
2. **Generate HTML export report?** — Toggle HTML report generation and optionally set a custom path
3. The export runs with a live spinner showing progress

## Import Workflow

1. **Filter content types?** — Select specific types or import all
2. **Dry run?** — Defaults to "Yes" for safety — preview what would be imported before committing changes
3. The import runs with a live spinner and prints a summary table

## Monitor Workflow

1. **Filter content types?** — Select specific types or monitor all
2. **Generate HTML change report?** — Toggle and set path
3. **Run on a schedule?** — Choose "Yes" to set a polling interval, or "No" for a one-shot comparison
4. If scheduled, press **Ctrl+C** to stop

## Audit Log Workflow

1. **How many days?** — Enter a number between 1 and 30 (default: 7)
2. **Generate HTML audit log report?** — Toggle and optionally set a custom path
3. **Generate JSON audit log report?** — Toggle and optionally set a custom path
4. The command fetches events from Microsoft Graph and prints a summary to the console

## Rollback Workflow

1. **Filter content types?** — Select specific types or rollback all
2. **Dry run?** — Defaults to "Yes" for safety — preview which policies would be reverted
3. The rollback compares live state against backup and restores drifted policies

## Diff Workflow

1. **Source backup path** — Enter the path to the older baseline backup root
2. **Target backup path** — Enter the path to the newer backup root
3. **Filter content types?** — Select specific types or diff all
4. **Generate HTML diff report?** — Toggle and set path
5. Compares the two snapshots offline and shows the differences

## Dependency Analysis Workflow

1. **Filter content types?** — Select specific types or analyze all
2. **Generate JSON report?** — Toggle and optionally set a custom path
3. Analyzes policy relationships across the backup

## Validate Workflow

1. Runs backup validation automatically against the configured backup path
2. Validates the structure and readability of backup files for stored content types (flags malformed or unreadable files)

## Settings Overview

Displays a tree view of the current configuration loaded from `appsettings.json` and environment variables:

```
Configuration
├── Authentication
│   ├── Tenant ID:  xxxxxxxx-xxxx-...
│   ├── Client ID:  xxxxxxxx-xxxx-...
│   └── Method:     Certificate
├── Backup
│   ├── Storage:    LocalFile
│   └── Path:       ./intune-backup
└── Monitor
    ├── Interval:    Run once
    ├── Min severity:Info
    └── Changes only:False
```

## Mixing Modes

The interactive menu and CLI modes are complementary:

- **No arguments** → Interactive menu with prompts and navigation
- **With arguments** → Direct CLI execution (great for scripts and CI/CD)

```bash
# Interactive
dotnet run

# Direct CLI
dotnet run -- export
dotnet run -- monitor --interval 30 --changes-only
```

Both modes produce the same output — spinners, summary tables, and HTML reports.
