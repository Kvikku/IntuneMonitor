# Configuration

All settings can come from three sources. Highest priority wins:

```
CLI flags  >  Environment variables  >  appsettings.json
```

## appsettings.json

Copy the example file and edit:

```bash
cp appsettings.example.json appsettings.json
```

> [!CAUTION]
> `appsettings.json` is in `.gitignore` — **never commit secrets**.

## Environment Variables

Environment variables use the `INTUNEMONITOR_` prefix with double-underscore separators for nested keys:

```bash
# Linux / macOS / CI
export INTUNEMONITOR_Authentication__TenantId="xxxxxxxx-..."
export INTUNEMONITOR_Authentication__ClientId="xxxxxxxx-..."
export INTUNEMONITOR_Authentication__ClientSecret="your-secret"
export INTUNEMONITOR_Backup__Path="/mnt/backup"
```

```powershell
# PowerShell
$env:INTUNEMONITOR_Authentication__TenantId = "xxxxxxxx-..."
$env:INTUNEMONITOR_Authentication__ClientId = "xxxxxxxx-..."
$env:INTUNEMONITOR_Authentication__ClientSecret = "your-secret"
```

## CLI Flags

Override any setting on a per-run basis:

```bash
dotnet run -- export --tenant-id "..." --client-id "..." --client-secret "..."
```

See [Commands — Global Options](commands.md#global-options) for the full list.

---

## Authentication Settings

| Key | Description | Default |
|---|---|---|
| `TenantId` | Entra tenant ID (GUID or domain) | — |
| `ClientId` | Application (client) ID | — |
| `Method` | `ClientSecret` or `Certificate` | `ClientSecret` |
| `ClientSecret` | Secret value | — |
| `CertificatePath` | Path to PFX or PEM file | — |
| `CertificatePassword` | PFX password (optional) | — |
| `CertificateThumbprint` | Thumbprint for Windows cert-store lookup | — |

See [Authentication](authentication.md) for detailed examples.

## Backup Settings

| Key | Description | Default |
|---|---|---|
| `StorageType` | `LocalFile` or `Git` | `LocalFile` |
| `Path` | Root directory for backups | `./intune-backup` |
| `SubDirectory` | Sub-directory within Path | — |
| `GitRemoteUrl` | Remote URL (Git storage) | — |
| `GitBranch` | Branch name | `main` |
| `GitUsername` | HTTPS username | — |
| `GitToken` | Personal access token | — |
| `GitAuthorName` | Commit author name | `IntuneMonitor` |
| `GitAuthorEmail` | Commit author email | `intune-monitor@noreply.local` |
| `AutoCommit` | Auto-push after export | `true` |
| `HtmlExportReportPath` | HTML export report output path | — |
| `OpenHtmlExportReport` | Auto-open export report in browser | `true` |

See [Git Storage](git-storage.md) for Git-specific configuration.

## Monitor Settings

| Key | Description | Default |
|---|---|---|
| `IntervalMinutes` | Polling interval. `0` = run once | `0` |
| `ChangesOnly` | Only output when changes exist | `false` |
| `ReportOutputPath` | JSON change report path | — |
| `HtmlReportOutputPath` | HTML dashboard path | — |
| `OpenHtmlReport` | Auto-open HTML report in browser | `true` |
| `MinSeverity` | Minimum severity: `Info`, `Warning`, `Critical` | `Info` |

See [Monitoring & Scheduling](monitoring.md) for details.

## Content Types

| Key | Description | Default |
|---|---|---|
| `ContentTypes` | List of content types to process | All types |

When the list is empty (default), all 13 content types are processed. Specify a subset to limit scope:

```json
{
  "ContentTypes": [
    "SettingsCatalog",
    "DeviceCompliancePolicy"
  ]
}
```

## Full Example

```jsonc
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "Certificate",
    "CertificateThumbprint": "AABBCCDDEEFF..."
  },
  "Backup": {
    "StorageType": "Git",
    "Path": "./intune-backup",
    "GitRemoteUrl": "https://github.com/your-org/intune-backup.git",
    "GitBranch": "main",
    "GitToken": "ghp_...",
    "AutoCommit": true,
    "HtmlExportReportPath": "reports/export-report.html"
  },
  "Monitor": {
    "IntervalMinutes": 60,
    "ChangesOnly": true,
    "HtmlReportOutputPath": "reports/change-report.html",
    "MinSeverity": "Warning"
  },
  "ContentTypes": []
}
```
