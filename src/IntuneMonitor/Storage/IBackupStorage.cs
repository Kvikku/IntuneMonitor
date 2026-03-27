namespace IntuneMonitor.Storage;

using IntuneMonitor.Models;

/// <summary>
/// Abstraction over backup storage backends.
/// </summary>
public interface IBackupStorage
{
    /// <summary>
    /// Saves a backup document for a specific content type.
    /// </summary>
    Task SaveBackupAsync(string contentType, BackupDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a previously saved backup document for a specific content type.
    /// Returns null if no backup exists for that content type.
    /// </summary>
    Task<BackupDocument?> LoadBackupAsync(string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the content types that currently have a backup stored.
    /// </summary>
    Task<IReadOnlyList<string>> ListStoredContentTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after all content types have been saved.
    /// Used by the Git backend to commit and push changes.
    /// </summary>
    Task FinalizeExportAsync(string commitMessage, CancellationToken cancellationToken = default);
}
