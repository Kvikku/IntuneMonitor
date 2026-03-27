using IntuneMonitor.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// <param name="loggerFactory">Optional logger factory for creating typed loggers.</param>
    /// <returns>An <see cref="IBackupStorage"/> implementation.</returns>
    /// <exception cref="NotSupportedException">Thrown for unknown storage types.</exception>
    public static IBackupStorage Create(BackupConfig config, ILoggerFactory? loggerFactory = null)
    {
        var factory = loggerFactory ?? NullLoggerFactory.Instance;
        return config.StorageType?.ToLowerInvariant() switch
        {
            "git" => new GitStorage(config, factory.CreateLogger<GitStorage>()),
            "localfile" or null or "" => new LocalFileStorage(config, factory.CreateLogger<LocalFileStorage>()),
            var unknown => throw new NotSupportedException(
                $"Unknown storage type '{unknown}'. Supported values: 'LocalFile', 'Git'.")
        };
    }
}
