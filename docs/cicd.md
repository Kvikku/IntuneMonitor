# CI/CD Integration

IntuneMonitor works in any CI/CD system that can run .NET 8. Below are examples for common platforms.

## GitHub Actions

### Scheduled Export to Git

```yaml
name: Intune Backup

on:
  schedule:
    - cron: '0 */6 * * *'  # Every 6 hours
  workflow_dispatch:         # Manual trigger

jobs:
  export:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build
        run: dotnet build
        working-directory: src/IntuneMonitor

      - name: Export Intune policies
        working-directory: src/IntuneMonitor
        env:
          INTUNEMONITOR_Authentication__TenantId: ${{ secrets.TENANT_ID }}
          INTUNEMONITOR_Authentication__ClientId: ${{ secrets.CLIENT_ID }}
          INTUNEMONITOR_Authentication__Method: ClientSecret
          INTUNEMONITOR_Authentication__ClientSecret: ${{ secrets.CLIENT_SECRET }}
          INTUNEMONITOR_Backup__StorageType: LocalFile
          INTUNEMONITOR_Backup__Path: ../../intune-backup
        run: dotnet run -- export

      - name: Commit backup
        run: |
          git config user.name "IntuneMonitor"
          git config user.email "intune-monitor@noreply.local"
          git add intune-backup/
          git diff --cached --quiet || git commit -m "Intune backup $(date -u +'%Y-%m-%d %H:%M:%S UTC')"
          git push
```

### Drift Detection on PR

```yaml
name: Intune Drift Check

on:
  pull_request:
    paths:
      - 'intune-backup/**'

jobs:
  monitor:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build
        run: dotnet build
        working-directory: src/IntuneMonitor

      - name: Check for drift
        working-directory: src/IntuneMonitor
        env:
          INTUNEMONITOR_Authentication__TenantId: ${{ secrets.TENANT_ID }}
          INTUNEMONITOR_Authentication__ClientId: ${{ secrets.CLIENT_ID }}
          INTUNEMONITOR_Authentication__Method: ClientSecret
          INTUNEMONITOR_Authentication__ClientSecret: ${{ secrets.CLIENT_SECRET }}
        run: dotnet run -- monitor --report-path ../../drift-report.json

      - name: Upload drift report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: drift-report
          path: drift-report.json
```

## Azure DevOps Pipelines

```yaml
trigger:
  - none

schedules:
  - cron: '0 */6 * * *'
    displayName: 'Every 6 hours'
    branches:
      include:
        - main
    always: true

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '8.0.x'

  - script: dotnet build
    workingDirectory: src/IntuneMonitor

  - script: dotnet run -- export --html-report $(Build.ArtifactStagingDirectory)/export-report.html
    workingDirectory: src/IntuneMonitor
    env:
      INTUNEMONITOR_Authentication__TenantId: $(TENANT_ID)
      INTUNEMONITOR_Authentication__ClientId: $(CLIENT_ID)
      INTUNEMONITOR_Authentication__ClientSecret: $(CLIENT_SECRET)

  - task: PublishBuildArtifacts@1
    inputs:
      pathToPublish: $(Build.ArtifactStagingDirectory)
      artifactName: 'intune-reports'
```

## Azure Automation Runbook

For running in an Azure Automation account with a managed identity or service principal:

1. **Publish as a self-contained app:**

   ```bash
   dotnet publish -c Release -r linux-x64 --self-contained
   ```

2. **Upload** the published output to a storage account or embed in the runbook
3. **Set environment variables** in the Automation account's variables (encrypted)
4. **Create a PowerShell runbook** that invokes the published executable

## Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/IntuneMonitor/IntuneMonitor.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./IntuneMonitor"]
```

```bash
docker build -t intune-monitor .
docker run --rm \
  -e INTUNEMONITOR_Authentication__TenantId="..." \
  -e INTUNEMONITOR_Authentication__ClientId="..." \
  -e INTUNEMONITOR_Authentication__ClientSecret="..." \
  intune-monitor export
```

## Security Best Practices

- **Never store secrets in code or config files** — use CI/CD secrets, Azure Key Vault, or environment variables
- Use **certificate authentication** over client secrets for production workloads
- Apply **least-privilege permissions** — use `Read.All` for export/monitor, only add `ReadWrite.All` when importing
- **Rotate secrets** on a regular schedule
- Use **managed identities** when running in Azure
