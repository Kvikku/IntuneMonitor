using IntuneMonitor.Models;
using IntuneMonitor.Reporting;

namespace IntuneMonitor.Tests;

public class CsvReportGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public CsvReportGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"intune-csv-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task WriteChangeReportAsync_WritesHeaderAndRows()
    {
        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = "test-tenant",
            Changes = new List<PolicyChange>
            {
                new PolicyChange
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "abc-123",
                    PolicyName = "Test Policy",
                    ChangeType = ChangeType.Modified,
                    Severity = ChangeSeverity.Warning,
                    FieldChanges = new List<FieldChange>
                    {
                        new FieldChange { FieldPath = "displayName", OldValue = "Old", NewValue = "New" }
                    }
                },
                new PolicyChange
                {
                    ContentType = "DeviceCompliancePolicy",
                    PolicyId = "def-456",
                    PolicyName = "Added Policy",
                    ChangeType = ChangeType.Added
                }
            }
        };

        var path = Path.Combine(_tempDir, "changes.csv");
        await CsvReportGenerator.WriteChangeReportAsync(report, path);

        Assert.True(File.Exists(path));
        var lines = await File.ReadAllLinesAsync(path);
        Assert.True(lines.Length >= 3); // header + 2 rows
        Assert.StartsWith("ContentType,", lines[0]);
        Assert.Contains("SettingsCatalog", lines[1]);
        Assert.Contains("DeviceCompliancePolicy", lines[2]);
    }

    [Fact]
    public async Task WriteExportReportAsync_WritesHeaderAndRows()
    {
        var report = new ExportReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = "test-tenant",
            StorageType = "LocalFile",
            BackupPath = "/tmp/backup",
            ContentSummaries = new List<ExportContentSummary>
            {
                new ExportContentSummary
                {
                    ContentType = "SettingsCatalog",
                    ItemCount = 2,
                    ItemNames = new List<string> { "Policy A", "Policy B" }
                }
            }
        };

        var path = Path.Combine(_tempDir, "export.csv");
        await CsvReportGenerator.WriteExportReportAsync(report, path);

        Assert.True(File.Exists(path));
        var lines = await File.ReadAllLinesAsync(path);
        Assert.True(lines.Length >= 3); // header + 2 item rows
        Assert.StartsWith("ContentType,", lines[0]);
        Assert.Contains("Policy A", lines[1]);
    }

    [Fact]
    public async Task WriteAuditReportAsync_WritesHeaderAndRows()
    {
        var report = new AuditLogReport
        {
            GeneratedAt = DateTime.UtcNow,
            TenantId = "test-tenant",
            DaysReviewed = 7,
            PeriodStart = DateTime.UtcNow.AddDays(-7),
            PeriodEnd = DateTime.UtcNow,
            TotalEvents = 1,
            Events = new List<AuditEvent>
            {
                new AuditEvent
                {
                    Id = "evt-1",
                    DisplayName = "Test Event",
                    ComponentName = "DeviceConfiguration",
                    Activity = "Update configuration",
                    ActivityType = "Update",
                    ActivityResult = "Success",
                    ActivityDateTime = DateTime.UtcNow,
                    Actor = new AuditActor { UserPrincipalName = "admin@test.com" },
                    Resources = new List<AuditResource>
                    {
                        new AuditResource { DisplayName = "Test Policy", ResourceType = "Configuration" }
                    }
                }
            }
        };

        var path = Path.Combine(_tempDir, "audit.csv");
        await CsvReportGenerator.WriteAuditReportAsync(report, path);

        Assert.True(File.Exists(path));
        var lines = await File.ReadAllLinesAsync(path);
        Assert.True(lines.Length >= 2); // header + 1 event
        Assert.StartsWith("DateTime,", lines[0]);
        Assert.Contains("admin@test.com", lines[1]);
    }

    [Fact]
    public async Task WriteChangeReportAsync_EscapesCsvSpecialChars()
    {
        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            Changes = new List<PolicyChange>
            {
                new PolicyChange
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "abc",
                    PolicyName = "Policy with, comma",
                    ChangeType = ChangeType.Added
                }
            }
        };

        var path = Path.Combine(_tempDir, "escaped.csv");
        await CsvReportGenerator.WriteChangeReportAsync(report, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\"Policy with, comma\"", content);
    }

    [Fact]
    public async Task WriteChangeReportAsync_EscapesFormulaInjection()
    {
        var report = new ChangeReport
        {
            GeneratedAt = DateTime.UtcNow,
            Changes = new List<PolicyChange>
            {
                new PolicyChange
                {
                    ContentType = "SettingsCatalog",
                    PolicyId = "abc",
                    PolicyName = "=cmd|'/c calc'!A1",
                    ChangeType = ChangeType.Added
                }
            }
        };

        var path = Path.Combine(_tempDir, "formula.csv");
        await CsvReportGenerator.WriteChangeReportAsync(report, path);

        var content = await File.ReadAllTextAsync(path);
        // Formula prefixes (=, +, -, @) must be quoted to prevent CSV injection
        Assert.Contains("\"=cmd|'/c calc'!A1\"", content);
    }
}
