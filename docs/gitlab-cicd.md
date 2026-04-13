# GitLab CI/CD Pipeline

This guide explains how IntuneMonitor runs as an automated pipeline in GitLab, including how the Entra app registration, certificates, and pipeline schedule work together.

## Overview

The pipeline runs on a schedule (every 6 hours) and performs three stages:

```
build → export → monitor
```

| Stage | What it does |
|---|---|
| **build** | Compiles the project and publishes a release binary as a pipeline artifact |
| **export** | Authenticates to Microsoft Graph, downloads all Intune policies, and commits the backup to the repo |
| **monitor** | Compares the live Intune state against the last backup and produces an HTML drift report |

The pipeline is defined in `.gitlab-ci.yml` in the repo root.

---

## Prerequisites

| Requirement | Where |
|---|---|
| Entra ID App Registration | Microsoft Entra admin center |
| X.509 Certificate (PFX) | Generated locally, public key uploaded to Entra |
| GitLab CI/CD Variables | Project settings |
| GitLab Pipeline Schedule | Project CI/CD schedules |
| GitLab Access Token | Project access tokens (for pushing backup commits) |

---

## Entra App Registration

The pipeline authenticates as a service principal using certificate-based auth.

### 1. Create the app registration

1. Go to **[Entra admin center](https://entra.microsoft.com)** → **Identity** → **Applications** → **App registrations** → **New registration**
2. **Name:** `IntuneMonitor-GitLab`
3. **Supported account types:** Single tenant
4. **Redirect URI:** leave blank
5. Click **Register**
6. Copy the **Application (client) ID** and **Directory (tenant) ID** from the Overview blade

### 2. Generate and upload a certificate

Generate a self-signed certificate on your workstation:

```powershell
$cert = New-SelfSignedCertificate -Subject "CN=IntuneMonitor-GitLab" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable -KeySpec Signature `
    -KeyLength 2048 -NotAfter (Get-Date).AddYears(2)

# Export PFX (private key) — this gets base64-encoded for GitLab
$pwd = ConvertTo-SecureString -String "YourPfxPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath .\intunemonitor.pfx -Password $pwd

# Export CER (public key) — upload this to Entra
Export-Certificate -Cert $cert -FilePath .\intunemonitor.cer
```

Upload the public key:
- In **Entra** → your app → **Certificates & secrets** → **Certificates** → **Upload certificate** → select `intunemonitor.cer`

Base64-encode the PFX for GitLab:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\intunemonitor.pfx"))
```

Copy the output — you'll paste it into a GitLab CI/CD variable.

### 3. Grant API permissions

In **Entra** → your app → **API permissions** → **Add a permission** → **Microsoft Graph** → **Application permissions**:

| Permission | Purpose |
|---|---|
| `DeviceManagementConfiguration.Read.All` | Settings catalog, compliance, device config, endpoint security, update policies |
| `DeviceManagementManagedDevices.Read.All` | PowerShell scripts, proactive remediations, macOS scripts |
| `DeviceManagementServiceConfig.Read.All` | Autopilot profiles, enrollment restrictions, role definitions |
| `DeviceManagementApps.Read.All` | App protection & app configuration policies |
| `Policy.Read.All` | Conditional access, named locations, assignment filters |
| `AuditLog.Read.All` | Audit log command |

> **Important:** These must be **Application** permissions (not Delegated).

Click **Grant admin consent for \<your tenant\>** and verify all rows show a green checkmark.

> If you later need to use the `import` command from CI, upgrade the first five to `.ReadWrite.All`.

---

## GitLab CI/CD Variables

Go to your GitLab project → **Settings** → **CI/CD** → **Variables** and add:

| Variable | Description | Protected | Masked |
|---|---|---|---|
| `TENANT_ID` | Entra directory (tenant) ID | ✅ | ✅ |
| `CLIENT_ID` | App registration application (client) ID | ✅ | ✅ |
| `CERTIFICATE_PFX_BASE64` | Base64-encoded PFX file (from the step above) | ✅ | ✅ |
| `CERTIFICATE_PASSWORD` | Password used when exporting the PFX | ✅ | ✅ |
| `PUSH_TOKEN` | GitLab access token with `write_repository` scope | ✅ | ✅ |

### Creating the push token

The export job commits backup files and pushes them to the repo. This requires a token with write access:

1. Go to **Settings** → **Access Tokens**
2. **Add new token** with:
   - **Role:** Maintainer
   - **Scopes:** `write_repository`
   - **Expiration:** set a reasonable date and track renewal
3. Copy the token value into the `PUSH_TOKEN` CI/CD variable

---

## Pipeline Schedule

Create a schedule to run the pipeline automatically:

1. Go to **Build** → **Pipeline schedules** → **New schedule**
2. **Description:** `Intune Backup`
3. **Interval pattern:** `0 0 * * *` (nightly at midnight)
4. **Target branch:** `main`
5. **Activated:** ✅

---

## How the Pipeline Works

### Build stage

- Uses the `dotnet/sdk:8.0` Docker image
- Runs `dotnet build` and `dotnet publish`
- Uploads the published binary as a pipeline artifact for downstream jobs

### Export stage

1. Decodes the base64 certificate into a PFX file inside the container
2. Authenticates to Microsoft Graph using the certificate
3. Downloads all 20 Intune content types as JSON
4. Commits the backup files to the repo and pushes using `PUSH_TOKEN`
5. Cleans up the PFX file

The backup is stored in the `intune-backup/` directory at the repo root.

### Monitor stage

1. Decodes the certificate (same as export)
2. Compares the live Intune state against the last backup
3. Generates an HTML drift report saved as a pipeline artifact
4. The report is available for download from the pipeline for 30 days

---

## Running Manually

### From GitLab

Go to **Build** → **Pipelines** → **Run pipeline** to trigger a manual run (the pipeline accepts `web` as a source).

### From your workstation

You can use the same certificate locally. If you imported it to your Windows certificate store during generation:

```powershell
# Find the thumbprint
Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq "CN=IntuneMonitor-GitLab"

# Run with the thumbprint
cd src/IntuneMonitor
dotnet run -- export --cert-thumbprint "YOUR_THUMBPRINT"
```

Or reference the PFX file directly in `appsettings.json`:

```json
{
  "Authentication": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Method": "Certificate",
    "CertificatePath": "C:\\path\\to\\intunemonitor.pfx",
    "CertificatePassword": "YourPfxPassword"
  }
}
```

> `appsettings.json` is gitignored — secrets stay local.

---

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| `401 Unauthorized` from Graph API | Missing permissions or admin consent not granted | Check Entra → API permissions → verify green checkmarks |
| `Skipping Git checkout` in job logs | Runner ignoring `GIT_STRATEGY` | The pipeline includes a fallback `git clone` in `before_script` |
| `git: command not found` | Job image missing git | All jobs use `dotnet/sdk` image which includes git |
| Job runs on wrong runner | No tag constraint | Pipeline uses `tags: [autoscale]` to pin to the Docker runner |
| `push` fails in export job | `PUSH_TOKEN` missing or expired | Create/renew the project access token and update the CI/CD variable |
| Certificate errors | PFX base64 corrupted or wrong password | Re-encode the PFX and verify `CERTIFICATE_PASSWORD` matches |

---

## Certificate Renewal

The self-signed certificate has an expiry (set to 2 years in the example). When it expires:

1. Generate a new certificate (same PowerShell commands as above)
2. Upload the new `.cer` to Entra (you can keep the old one until rotation is complete)
3. Base64-encode the new `.pfx` and update `CERTIFICATE_PFX_BASE64` in GitLab
4. Update `CERTIFICATE_PASSWORD` if changed
5. Remove the old certificate from Entra once the new one is verified working
