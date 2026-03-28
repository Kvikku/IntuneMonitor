using System.Text.Json;
using IntuneMonitor.Models;
using IntuneMonitor.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntuneMonitor.Tests;

public class BackupValidatorTests
{
    private readonly BackupValidator _validator = new(NullLogger<BackupValidator>.Instance);

    [Fact]
    public void Validate_ValidDocument_ReturnsValid()
    {
        var doc = new BackupDocument
        {
            ExportedAt = DateTime.UtcNow.ToString("o"),
            TenantId = "tenant-1",
            ContentType = "SettingsCatalog",
            Items = new List<IntuneItem>
            {
                new IntuneItem
                {
                    Id = "item-1",
                    Name = "Test Policy",
                    ContentType = "SettingsCatalog",
                    PolicyData = JsonSerializer.Deserialize<JsonElement>("{\"displayName\": \"Test\"}")
                }
            }
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingContentType_ReturnsError()
    {
        var doc = new BackupDocument
        {
            ExportedAt = DateTime.UtcNow.ToString("o"),
            ContentType = "",
            Items = new List<IntuneItem>
            {
                new IntuneItem
                {
                    Id = "item-1",
                    Name = "Test",
                    PolicyData = JsonSerializer.Deserialize<JsonElement>("{\"displayName\": \"Test\"}")
                }
            }
        };

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ContentType"));
    }

    [Fact]
    public void Validate_MissingPolicyData_ReturnsError()
    {
        var doc = new BackupDocument
        {
            ContentType = "SettingsCatalog",
            Items = new List<IntuneItem>
            {
                new IntuneItem { Id = "item-1", Name = "No Data" }
            }
        };

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PolicyData"));
    }

    [Fact]
    public void Validate_DuplicateIds_ReturnsError()
    {
        var policyData = JsonSerializer.Deserialize<JsonElement>("{\"displayName\": \"Test\"}");
        var doc = new BackupDocument
        {
            ContentType = "SettingsCatalog",
            Items = new List<IntuneItem>
            {
                new IntuneItem { Id = "dup-id", Name = "Policy 1", PolicyData = policyData },
                new IntuneItem { Id = "dup-id", Name = "Policy 2", PolicyData = policyData }
            }
        };

        var result = _validator.Validate(doc);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_EmptyItems_ReturnsWarning()
    {
        var doc = new BackupDocument
        {
            ContentType = "SettingsCatalog",
            ExportedAt = DateTime.UtcNow.ToString("o"),
            Items = new List<IntuneItem>()
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("no items"));
    }

    [Fact]
    public void Validate_MismatchedContentType_ReturnsWarning()
    {
        var policyData = JsonSerializer.Deserialize<JsonElement>("{\"displayName\": \"Test\"}");
        var doc = new BackupDocument
        {
            ContentType = "SettingsCatalog",
            Items = new List<IntuneItem>
            {
                new IntuneItem
                {
                    Id = "item-1",
                    Name = "Test",
                    ContentType = "DeviceCompliancePolicy",
                    PolicyData = policyData
                }
            }
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("doesn't match"));
    }
}
