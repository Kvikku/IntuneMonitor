# Getting Started

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Runtime for building and running the app |
| Entra ID App Registration | With the Microsoft Graph permissions listed below |

## Required API Permissions

Register an application in **Microsoft Entra ID** → **App registrations** and grant the following **Application** permissions under **Microsoft Graph**:

| Permission | Needed for |
|---|---|
| `DeviceManagementConfiguration.Read.All` | Export & Monitor |
| `DeviceManagementConfiguration.ReadWrite.All` | Import |
| `DeviceManagementApps.Read.All` | Export & Monitor |
| `DeviceManagementManagedDevices.Read.All` | Export & Monitor |
| `DeviceManagementServiceConfig.Read.All` | Export & Monitor |
| `DeviceManagementRBAC.Read.All` | Audit Log |

> [!IMPORTANT]
> After adding permissions, click **Grant admin consent** in the Azure Portal.

## Clone & Build

```bash
git clone https://github.com/Kvikku/IntuneMonitor.git
cd IntuneMonitor/src/IntuneMonitor
dotnet build
```

## Configure

```bash
cp appsettings.example.json appsettings.json
```

Fill in your Entra ID app registration details:

```jsonc
{
  "Authentication": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Method": "ClientSecret",
    "ClientSecret": "your-secret-here"
  },
  "Backup": {
    "StorageType": "LocalFile",
    "Path": "./intune-backup"
  }
}
```

> [!CAUTION]
> `appsettings.json` is in `.gitignore` — **never commit secrets**.
> Use environment variables in CI/CD (see [Configuration](configuration.md)).

## First Export

```bash
dotnet run -- export
```

This downloads all supported Intune policy types to JSON files in `./intune-backup/`.

## What's Next?

- [Run interactively](interactive-mode.md) with the menu-driven UI
- [Detect drift](monitoring.md) with the monitor command
- [Set up Git storage](git-storage.md) for version-controlled backups
- [Configure CI/CD](cicd.md) for automated exports
