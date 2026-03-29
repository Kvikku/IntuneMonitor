using IntuneMonitor.Config;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for BackupStorageFactory routing logic.
/// </summary>
public class BackupStorageFactoryTests
{
    private static BackupConfig MakeConfig(string? storageType = null) =>
        new()
        {
            StorageType = storageType ?? "LocalFile",
            Path = Path.GetTempPath()
        };

    [Fact]
    public void Create_LocalFile_ReturnsLocalFileStorage()
    {
        var storage = BackupStorageFactory.Create(MakeConfig("LocalFile"));
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Theory]
    [InlineData("localfile")]
    [InlineData("LOCALFILE")]
    [InlineData("LocalFile")]
    public void Create_LocalFile_CaseInsensitive(string storageType)
    {
        var storage = BackupStorageFactory.Create(MakeConfig(storageType));
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Fact]
    public void Create_NullStorageType_DefaultsToLocalFile()
    {
        var config = new BackupConfig { StorageType = null!, Path = Path.GetTempPath() };
        var storage = BackupStorageFactory.Create(config);
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Fact]
    public void Create_EmptyStorageType_DefaultsToLocalFile()
    {
        var config = new BackupConfig { StorageType = "", Path = Path.GetTempPath() };
        var storage = BackupStorageFactory.Create(config);
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Fact]
    public void Create_Git_ReturnsGitStorage()
    {
        var storage = BackupStorageFactory.Create(MakeConfig("Git"));
        Assert.IsType<GitStorage>(storage);
    }

    [Theory]
    [InlineData("git")]
    [InlineData("GIT")]
    [InlineData("Git")]
    public void Create_Git_CaseInsensitive(string storageType)
    {
        var storage = BackupStorageFactory.Create(MakeConfig(storageType));
        Assert.IsType<GitStorage>(storage);
    }

    [Fact]
    public void Create_UnknownType_ThrowsNotSupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            BackupStorageFactory.Create(MakeConfig("CosmosDB")));

        Assert.Contains("cosmosdb", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supported values", ex.Message);
    }
}
