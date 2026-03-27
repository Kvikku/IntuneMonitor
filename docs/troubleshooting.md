# Troubleshooting

## Authentication Errors

### "Authentication error" on startup

**Cause:** Missing or incorrect credentials.

**Fix:**
- Verify `TenantId` and `ClientId` in `appsettings.json` match your Entra app registration
- For client secrets: ensure the secret hasn't expired
- For certificates: verify the file path exists and the password is correct
- Check that the app registration has admin consent granted for the required permissions

### "Insufficient privileges" or 403 errors

**Cause:** Missing API permissions.

**Fix:**
- Go to **Azure Portal** → **Entra ID** → **App registrations** → your app → **API permissions**
- Ensure all [required permissions](getting-started.md#required-api-permissions) are added
- Click **Grant admin consent** — the status should show green checkmarks

### Certificate not found (thumbprint)

**Cause:** The certificate isn't installed in the expected store.

**Fix:**
- IntuneMonitor searches `CurrentUser\My` and `LocalMachine\My`
- Verify: `certmgr.msc` (CurrentUser) or `certlm.msc` (LocalMachine)
- On Linux, certificate store lookups are not supported — use `CertificatePath` instead

---

## Export Issues

### "Export error" with 0 items

**Cause:** The Graph API returned an error or the permissions don't cover the requested content types.

**Fix:**
- Run with `--verbosity Debug` to see detailed Graph API calls
- Verify the app has `DeviceManagementConfiguration.Read.All` and other read permissions
- Check if the content type exists in your tenant (e.g., macOS scripts require macOS management)

### "Storage configuration error"

**Cause:** Invalid backup path or Git configuration.

**Fix:**
- Verify the `Backup.Path` directory is writable
- For Git storage, check `GitRemoteUrl`, `GitUsername`, and `GitToken`

---

## Monitor Issues

### "No changes detected" when changes exist

**Cause:** The backup is up-to-date (you exported recently).

**Fix:**
- The monitor compares live state against the **latest backup**
- If you just ran `export`, the backup matches the live state
- Make a change in Intune, then run `monitor` to detect it

### Changes not showing at expected severity

**Cause:** `MinSeverity` filter is set too high.

**Fix:**
- Check `Monitor.MinSeverity` in `appsettings.json`
- Set to `"Info"` to see all changes
- Remember: Added = Info, Modified = Warning, Removed = Critical

---

## Import Issues

### "Skipping — no policy data in backup"

**Cause:** The backup item was exported without full policy data.

**Fix:**
- Re-run `export` to get a fresh backup with complete data
- This can happen if the original export encountered a partial Graph API error

### Items failing to import

**Cause:** The target tenant may already have policies with conflicting names, or the app lacks write permissions.

**Fix:**
- Ensure the app has `DeviceManagementConfiguration.ReadWrite.All`
- Run with `--verbosity Debug` to see the exact Graph API error
- Use `--dry-run` first to preview what will be created

---

## Interactive Menu Issues

### Menu doesn't appear / garbled output

**Cause:** Terminal doesn't support ANSI escape codes.

**Fix:**
- Use a modern terminal: Windows Terminal, iTerm2, or any terminal with ANSI support
- Older terminals (e.g., `cmd.exe`) may not render Spectre.Console output correctly
- For CI/CD or non-interactive environments, always pass a command argument (e.g., `dotnet run -- export`)

---

## General

### "NETSDK1057: You are using a preview version of .NET"

**Info only** — this is a warning, not an error. The app runs fine on .NET 8 previews and stable releases.

### Getting more detail

Add `--verbosity Debug` or `--verbosity Trace` to any command:

```bash
dotnet run -- export --verbosity Debug
```

This shows per-item Graph API calls, detailed timing, and internal state.
