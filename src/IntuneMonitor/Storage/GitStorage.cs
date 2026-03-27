using System.Diagnostics;
using System.Text.Json;
using IntuneMonitor.Config;
using IntuneMonitor.Models;

namespace IntuneMonitor.Storage;

/// <summary>
/// Stores and loads Intune backups as JSON files within a local Git repository.
/// After each export, changes are committed and (optionally) pushed to the configured remote.
/// </summary>
public class GitStorage : IBackupStorage
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BackupConfig _config;
    private readonly string _repoPath;
    private readonly string _backupPath;

    public GitStorage(BackupConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _repoPath = config.Path;
        _backupPath = string.IsNullOrWhiteSpace(config.SubDirectory)
            ? config.Path
            : Path.Combine(config.Path, config.SubDirectory);
    }

    public async Task SaveBackupAsync(
        string contentType,
        BackupDocument document,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupPath);

        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var filePath = Path.Combine(_backupPath, fileName);
        var json = JsonSerializer.Serialize(document, WriteOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task<BackupDocument?> LoadBackupAsync(
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IntuneContentTypes.FileNames.TryGetValue(contentType, out var fileName))
            fileName = $"{contentType.ToLowerInvariant()}.json";

        var filePath = Path.Combine(_backupPath, fileName);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<BackupDocument>(json, ReadOptions);
    }

    public Task<IReadOnlyList<string>> ListStoredContentTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = new List<string>();

        if (!Directory.Exists(_backupPath))
            return Task.FromResult<IReadOnlyList<string>>(stored);

        foreach (var kvp in IntuneContentTypes.FileNames)
        {
            var filePath = Path.Combine(_backupPath, kvp.Value);
            if (File.Exists(filePath))
                stored.Add(kvp.Key);
        }

        return Task.FromResult<IReadOnlyList<string>>(stored);
    }

    public async Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default)
    {
        // Ensure the repo is initialised
        EnsureGitRepoInitialized();

        // Pull latest (if remote configured)
        if (!string.IsNullOrWhiteSpace(_config.GitRemoteUrl))
        {
            await RunGitCommandAsync("fetch origin", cancellationToken);
        }

        // Stage all changes
        await RunGitCommandAsync($"add \"{_backupPath}\"", cancellationToken);

        // Check if there's anything to commit
        var statusOutput = await RunGitCommandAsync("status --porcelain", cancellationToken);
        if (string.IsNullOrWhiteSpace(statusOutput))
        {
            Console.WriteLine("Git: No changes to commit.");
            return;
        }

        // Commit
        var author = $"--author=\"{_config.GitAuthorName} <{_config.GitAuthorEmail}>\"";
        var safeMessage = commitMessage.Replace("\"", "\\\"");
        await RunGitCommandAsync($"commit {author} -m \"{safeMessage}\"", cancellationToken);

        // Push if remote and AutoCommit are configured
        if (_config.AutoCommit && !string.IsNullOrWhiteSpace(_config.GitRemoteUrl))
        {
            await RunGitCommandAsync($"push origin {_config.GitBranch}", cancellationToken);
            Console.WriteLine($"Git: Changes pushed to remote ({_config.GitBranch}).");
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void EnsureGitRepoInitialized()
    {
        Directory.CreateDirectory(_repoPath);

        var gitDir = Path.Combine(_repoPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            RunGitCommandSync("init");

            if (!string.IsNullOrWhiteSpace(_config.GitRemoteUrl))
            {
                RunGitCommandSync($"remote add origin \"{_config.GitRemoteUrl}\"");
                RunGitCommandSync($"checkout -b {_config.GitBranch}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(_config.GitRemoteUrl))
        {
            // Ensure the remote is set
            var remoteOutput = RunGitCommandSync("remote -v");
            if (!remoteOutput.Contains(_config.GitRemoteUrl, StringComparison.OrdinalIgnoreCase))
            {
                RunGitCommandSync($"remote set-url origin \"{_config.GitRemoteUrl}\"");
            }
        }
    }

    private async Task<string> RunGitCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        var env = BuildGitEnvironment();
        return await Task.Run(() => RunGit(arguments, env), cancellationToken);
    }

    private string RunGitCommandSync(string arguments)
    {
        var env = BuildGitEnvironment();
        return RunGit(arguments, env);
    }

    private Dictionary<string, string> BuildGitEnvironment()
    {
        var env = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(_config.GitUsername) && !string.IsNullOrWhiteSpace(_config.GitToken))
        {
            // Configure credential helper via environment
            env["GIT_ASKPASS"] = "echo";
            env["GIT_USERNAME"] = _config.GitUsername;
            env["GIT_PASSWORD"] = _config.GitToken;
        }

        env["GIT_AUTHOR_NAME"] = _config.GitAuthorName;
        env["GIT_AUTHOR_EMAIL"] = _config.GitAuthorEmail;
        env["GIT_COMMITTER_NAME"] = _config.GitAuthorName;
        env["GIT_COMMITTER_EMAIL"] = _config.GitAuthorEmail;

        return env;
    }

    private string RunGit(string arguments, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var (key, value) in env)
            psi.EnvironmentVariables[key] = value;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {arguments} failed (exit {process.ExitCode}): {stderr}");

        return stdout.Trim();
    }
}
