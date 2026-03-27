# Monitoring & Scheduling

The `monitor` command compares the live Intune state against your last backup and reports configuration drift.

## How Change Detection Works

For each content type, the monitor:

1. **Fetches** the current live policies from Microsoft Graph
2. **Loads** the corresponding backup from storage
3. **Compares** each policy by ID and performs a deep JSON diff

### Change Types

| Type | Description | Default Severity |
|---|---|---|
| **Added** | Policy exists live but not in backup | Info |
| **Removed** | Policy exists in backup but not live | Critical |
| **Modified** | Same policy ID, different content | Warning |

### Field-Level Diffing

Modified policies are compared at the field level. The diff engine:

- Performs deep JSON comparison of all properties
- Ignores auto-updated fields: `lastModifiedDateTime`, `version`, `@odata.type`
- Handles assignment arrays specially — producing human-readable target names (e.g., "Include: Security Team")
- Shows up to 10 field changes per policy in console output

## Severity Levels

Filter the minimum severity reported with `MinSeverity`:

```json
{
  "Monitor": {
    "MinSeverity": "Warning"
  }
}
```

| Level | Meaning |
|---|---|
| `Info` | Report everything (default) |
| `Warning` | Only modified and removed policies |
| `Critical` | Only removed policies |

## Scheduling

### Run Once (Default)

```bash
dotnet run -- monitor
```

### Built-in Polling Loop

```bash
dotnet run -- monitor --interval 30
```

The process stays alive and re-runs the comparison every 30 minutes. Press **Ctrl+C** to stop gracefully.

### External Schedulers

Set `IntervalMinutes = 0` and use your platform's scheduler:

**Windows Task Scheduler:**

```
Program: dotnet
Arguments: run -- monitor --html-report ./report.html
Start in: C:\path\to\IntuneMonitor\src\IntuneMonitor
```

**Linux cron:**

```cron
0 * * * *  cd /opt/IntuneMonitor/src/IntuneMonitor && dotnet run -- monitor --report-path /var/log/intune-drift.json
```

**Azure Automation:** See [CI/CD Integration](cicd.md).

## Changes-Only Mode

Suppress console output when nothing has changed:

```bash
dotnet run -- monitor --interval 60 --changes-only
```

Useful for long-running scheduled monitoring where you only care about drift.

## Reports

### JSON Report

```bash
dotnet run -- monitor --report-path ./drift-report.json
```

The JSON report contains the full `ChangeReport` object with all changes, field-level diffs, and metadata. Useful for further processing or integration with alerting systems.

### HTML Report

```bash
dotnet run -- monitor --html-report ./change-report.html
```

Generates a self-contained HTML dashboard with:

- Summary cards (total changes, added/modified/removed counts)
- Change tables grouped by content type
- Severity badges (Info / Warning / Critical)
- Field-level before/after details
- Light and dark theme toggle

By default, the HTML report auto-opens in the browser. Disable with:

```json
{
  "Monitor": {
    "OpenHtmlReport": false
  }
}
```

## Console Output

The monitor prints a styled change report to the terminal:

- **Summary table** with added/modified/removed counts
- **Detail tables** grouped by content type with change icons:
  - `✚` Added (green)
  - `✎` Modified (yellow)
  - `✖` Removed (red)
- Severity badges
- Field change counts per policy
