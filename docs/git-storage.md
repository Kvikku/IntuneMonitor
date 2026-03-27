# Git Storage

Turn every export into a versioned Git commit — perfect for audit trails and rollbacks.

## Configuration

```json
{
  "Backup": {
    "StorageType": "Git",
    "Path": "./intune-backup-repo",
    "GitRemoteUrl": "https://github.com/your-org/intune-backup.git",
    "GitBranch": "main",
    "GitUsername": "git-user",
    "GitToken": "ghp_...",
    "AutoCommit": true
  }
}
```

## How It Works

When `StorageType` is set to `Git`:

1. **First run** — If the target directory doesn't exist, IntuneMonitor will:
   - `git init` a new repository
   - Add the configured remote
   - Create the configured branch
2. **Each export** — Policy JSON files are written to the repository directory
3. **Auto-commit** — If `AutoCommit` is `true`, changes are committed with a timestamped message and pushed to the remote

Commit messages follow the format:

```
Intune export 2026-03-27 14:30:00 UTC – 42 items
```

## Authentication

The Git backend supports HTTPS authentication via username + personal access token:

| Setting | Description |
|---|---|
| `GitUsername` | HTTPS username (often `git` for GitHub/Azure DevOps) |
| `GitToken` | Personal access token with repo write permissions |

### GitHub

```json
{
  "GitUsername": "git",
  "GitToken": "ghp_xxxxxxxxxxxxxxxxxxxx"
}
```

### Azure DevOps

```json
{
  "GitRemoteUrl": "https://dev.azure.com/your-org/your-project/_git/intune-backup",
  "GitUsername": "git",
  "GitToken": "your-ado-pat"
}
```

## Commit Metadata

| Setting | Default | Description |
|---|---|---|
| `GitAuthorName` | `IntuneMonitor` | Author name on commits |
| `GitAuthorEmail` | `intune-monitor@noreply.local` | Author email on commits |

## Branching

Set `GitBranch` to work on a specific branch:

```json
{
  "GitBranch": "intune-backups"
}
```

## Using with Monitor

The monitor command reads from the same Git-backed storage. This means you can:

1. **Export** to Git (automatic commit + push)
2. **Monitor** against the Git-backed baseline
3. Review change history in your Git hosting platform

```bash
# Export and push to Git
dotnet run -- export

# Later, detect drift against the committed backup
dotnet run -- monitor --html-report ./drift-report.html
```

## Local File vs. Git

| Feature | LocalFile | Git |
|---|---|---|
| Setup complexity | None | Requires remote + token |
| Version history | Manual (timestamped folders) | Automatic Git commits |
| Remote backup | No | Yes (pushed to remote) |
| Audit trail | Limited | Full Git log |
| Rollback | Copy files manually | `git checkout` any commit |
