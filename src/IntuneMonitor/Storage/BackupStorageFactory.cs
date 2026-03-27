using IntuneMonitor.Config;

namespace IntuneMonitor.Storage;

/// <summary>
/// Factory that creates the appropriate <see cref="IBackupStorage"/> backend
/// based on the configured <see cref="BackupConfig.StorageType"/>.
/// </summary>
public static class BackupStorageFactory
{
    /// <summary>
    /// Creates a storage backend from the provided configuration.
    /// </summary>
    /// <param name="config">Backup configuration.</param>
    /// <returns>An <see cref="IBackupStorage"/> implementation.</returns>
    /// <exception cref="NotSupportedException">Thrown for unknown storage types.</exception>
    public static IBackupStorage Create(BackupConfig config)
    {
        return config.StorageType?.ToLowerInvariant() switch
        {
            "git" => new GitStorage(config),
            "localfile" or null or "" => new LocalFileStorage(config),
            var unknown => throw new NotSupportedException(
                $"Unknown storage type '{unknown}'. Supported values: 'LocalFile', 'Git'.")
        };
    }
}
