using System.Text.Json;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for <see cref="EntraSnapshotStorage"/>: saving and loading Entra snapshots
/// to/from local timestamped folders.
/// </summary>
public class EntraSnapshotStorageTests : IDisposable
{
    private readonly string _tempDir;

    public EntraSnapshotStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"entra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static EntraAppItem MakeItem(string id, string name)
    {
        var json = JsonSerializer.Serialize(new { id, displayName = name });
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        return new EntraAppItem { Id = id, DisplayName = name, Data = data };
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var storage = new EntraSnapshotStorage(_tempDir);
        var snapshot = new EntraSnapshot
        {
            AppRegistrations = new() { MakeItem("1", "App A") },
            EnterpriseApplications = new() { MakeItem("2", "EA B") }
        };

        await storage.SaveSnapshotAsync(snapshot);

        var loaded = await storage.LoadLatestSnapshotAsync();

        Assert.NotNull(loaded);
        Assert.Single(loaded!.AppRegistrations);
        Assert.Equal("1", loaded.AppRegistrations[0].Id);
        Assert.Equal("App A", loaded.AppRegistrations[0].DisplayName);
        Assert.Single(loaded.EnterpriseApplications);
        Assert.Equal("2", loaded.EnterpriseApplications[0].Id);
    }

    [Fact]
    public async Task LoadLatestSnapshot_NoSnapshots_ReturnsNull()
    {
        var storage = new EntraSnapshotStorage(_tempDir);

        var loaded = await storage.LoadLatestSnapshotAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadLatestSnapshot_NonExistentDir_ReturnsNull()
    {
        var storage = new EntraSnapshotStorage(Path.Combine(_tempDir, "nonexistent"));

        var loaded = await storage.LoadLatestSnapshotAsync();

        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveSnapshot_CreatesTimestampedFolder()
    {
        var storage = new EntraSnapshotStorage(_tempDir);
        var snapshot = new EntraSnapshot
        {
            AppRegistrations = new(),
            EnterpriseApplications = new()
        };

        var path = await storage.SaveSnapshotAsync(snapshot);

        Assert.True(Directory.Exists(path));
        Assert.True(File.Exists(Path.Combine(path, "app-registrations.json")));
        Assert.True(File.Exists(Path.Combine(path, "enterprise-applications.json")));
    }

    [Fact]
    public async Task LoadLatestSnapshot_MultipleFolders_ReturnsNewest()
    {
        var storage = new EntraSnapshotStorage(_tempDir);

        // Create an older snapshot folder directly with a fixed past timestamp (no wall-clock delay needed)
        var olderFolder = Path.Combine(_tempDir, "2020-01-01_000000");
        Directory.CreateDirectory(olderFolder);
        var oldItems = new List<EntraAppItem> { MakeItem("1", "First") };
        await File.WriteAllTextAsync(
            Path.Combine(olderFolder, "app-registrations.json"),
            System.Text.Json.JsonSerializer.Serialize(oldItems));
        await File.WriteAllTextAsync(
            Path.Combine(olderFolder, "enterprise-applications.json"),
            System.Text.Json.JsonSerializer.Serialize(new List<EntraAppItem>()));

        // Save a second snapshot via the normal path (its timestamp will be after 2020)
        var snapshot2 = new EntraSnapshot
        {
            AppRegistrations = new() { MakeItem("2", "Second") },
            EnterpriseApplications = new()
        };
        await storage.SaveSnapshotAsync(snapshot2);

        var loaded = await storage.LoadLatestSnapshotAsync();

        Assert.NotNull(loaded);
        Assert.Single(loaded!.AppRegistrations);
        Assert.Equal("Second", loaded.AppRegistrations[0].DisplayName);
    }

    [Fact]
    public async Task SaveSnapshot_PreservesJsonData()
    {
        var storage = new EntraSnapshotStorage(_tempDir);
        var item = MakeItem("1", "App A");
        var snapshot = new EntraSnapshot
        {
            AppRegistrations = new() { item },
            EnterpriseApplications = new()
        };

        await storage.SaveSnapshotAsync(snapshot);
        var loaded = await storage.LoadLatestSnapshotAsync();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.AppRegistrations[0].Data);
        Assert.Equal("App A", loaded.AppRegistrations[0].Data!.Value.GetProperty("displayName").GetString());
    }
}
