using System.Text.Json;
using IntuneMonitor.Config;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for LocalFileStorage using temporary directories.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _tempDir;

    public LocalFileStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "IntuneMonitorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private LocalFileStorage CreateStorage(string? subDirectory = null)
    {
        var config = new BackupConfig
        {
            Path = _tempDir,
            SubDirectory = subDirectory ?? string.Empty
        };
        return new LocalFileStorage(config);
    }

    private static BackupDocument MakeBackup(string contentType, params IntuneItem[] items) =>
        new()
        {
            ContentType = contentType,
            TenantId = "test-tenant",
            Items = items.ToList()
        };

    private static IntuneItem MakeItem(string id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            ContentType = IntuneContentTypes.SettingsCatalog,
            PolicyData = JsonSerializer.Deserialize<JsonElement>(
                $$"""{"id":"{{id}}","displayName":"{{name}}","enabled":true}""")
        };

    // -----------------------------------------------------------------------
    // SaveBackupAsync + LoadBackupAsync round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesItems()
    {
        var storage = CreateStorage();
        var item = MakeItem("p1", "Policy One");
        var doc = MakeBackup(IntuneContentTypes.SettingsCatalog, item);

        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, doc);

        var loaded = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
        Assert.Equal("p1", loaded.Items[0].Id);
        Assert.Equal("Policy One", loaded.Items[0].Name);
        Assert.NotNull(loaded.Items[0].PolicyData);
    }

    [Fact]
    public async Task SaveAndLoad_MultipleItems_AllPreserved()
    {
        var storage = CreateStorage();
        var items = new[]
        {
            MakeItem("p1", "Policy One"),
            MakeItem("p2", "Policy Two"),
            MakeItem("p3", "Policy Three"),
        };
        var doc = MakeBackup(IntuneContentTypes.SettingsCatalog, items);

        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, doc);

        var loaded = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Items.Count);
    }

    [Fact]
    public async Task SaveAndLoad_MultipleContentTypes_IndependentlyStored()
    {
        var storage = CreateStorage();

        var settingsDoc = MakeBackup(IntuneContentTypes.SettingsCatalog,
            MakeItem("s1", "Settings Policy"));
        var complianceDoc = MakeBackup(IntuneContentTypes.DeviceCompliancePolicy,
            MakeItem("c1", "Compliance Policy"));

        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, settingsDoc);
        await storage.SaveBackupAsync(IntuneContentTypes.DeviceCompliancePolicy, complianceDoc);

        var loadedSettings = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);
        var loadedCompliance = await storage.LoadBackupAsync(IntuneContentTypes.DeviceCompliancePolicy);

        Assert.NotNull(loadedSettings);
        Assert.Single(loadedSettings!.Items);
        Assert.Equal("s1", loadedSettings.Items[0].Id);

        Assert.NotNull(loadedCompliance);
        Assert.Single(loadedCompliance!.Items);
        Assert.Equal("c1", loadedCompliance.Items[0].Id);
    }

    // -----------------------------------------------------------------------
    // LoadBackupAsync edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoadBackup_NoRunFolder_ReturnsNull()
    {
        var storage = CreateStorage();
        var result = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadBackup_EmptyRunFolder_ReturnsNull()
    {
        // Create a timestamped folder but no content type subfolder
        Directory.CreateDirectory(Path.Combine(_tempDir, "2024-01-01_120000"));

        var storage = CreateStorage();
        var result = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadBackup_EmptyContentFolder_ReturnsNull()
    {
        // Create a timestamped folder with an empty content type subfolder
        var runDir = Path.Combine(_tempDir, "2024-01-01_120000");
        Directory.CreateDirectory(Path.Combine(runDir, "SettingsCatalog"));

        var storage = CreateStorage();
        var result = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // ListStoredContentTypesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListStoredContentTypes_EmptyStorage_ReturnsEmpty()
    {
        var storage = CreateStorage();
        var types = await storage.ListStoredContentTypesAsync();
        Assert.Empty(types);
    }

    [Fact]
    public async Task ListStoredContentTypes_AfterSave_ReturnsCorrectTypes()
    {
        var storage = CreateStorage();

        var doc = MakeBackup(IntuneContentTypes.SettingsCatalog,
            MakeItem("p1", "Policy One"));
        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, doc);

        var types = await storage.ListStoredContentTypesAsync();

        Assert.Single(types);
        Assert.Contains(IntuneContentTypes.SettingsCatalog, types);
    }

    [Fact]
    public async Task ListStoredContentTypes_MultipleTypes_AllReturned()
    {
        var storage = CreateStorage();

        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog,
            MakeBackup(IntuneContentTypes.SettingsCatalog, MakeItem("s1", "S")));
        await storage.SaveBackupAsync(IntuneContentTypes.DeviceCompliancePolicy,
            MakeBackup(IntuneContentTypes.DeviceCompliancePolicy, MakeItem("c1", "C")));

        var types = await storage.ListStoredContentTypesAsync();

        Assert.Equal(2, types.Count);
        Assert.Contains(IntuneContentTypes.SettingsCatalog, types);
        Assert.Contains(IntuneContentTypes.DeviceCompliancePolicy, types);
    }

    // -----------------------------------------------------------------------
    // SubDirectory support
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SubDirectory_StorageSavesInSubDir()
    {
        var storage = CreateStorage("nested/sub");

        var doc = MakeBackup(IntuneContentTypes.SettingsCatalog,
            MakeItem("p1", "Policy One"));
        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, doc);

        var loaded = await storage.LoadBackupAsync(IntuneContentTypes.SettingsCatalog);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
    }

    // -----------------------------------------------------------------------
    // File naming
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveBackup_CreatesJsonFiles()
    {
        var storage = CreateStorage();
        var doc = MakeBackup(IntuneContentTypes.SettingsCatalog,
            MakeItem("abc-def-123", "My Test Policy"));

        await storage.SaveBackupAsync(IntuneContentTypes.SettingsCatalog, doc);

        // Verify files were created
        var runDirs = Directory.GetDirectories(_tempDir);
        Assert.Single(runDirs);

        var contentDir = Path.Combine(runDirs[0], "SettingsCatalog");
        Assert.True(Directory.Exists(contentDir));

        var files = Directory.GetFiles(contentDir, "*.json");
        Assert.Single(files);
        Assert.Contains("My Test Policy", Path.GetFileName(files[0]));
    }

    // -----------------------------------------------------------------------
    // FinalizeExportAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizeExport_DoesNotThrow()
    {
        var storage = CreateStorage();
        await storage.FinalizeExportAsync("test commit");
    }

    // -----------------------------------------------------------------------
    // Constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalFileStorage(null!));
    }
}
