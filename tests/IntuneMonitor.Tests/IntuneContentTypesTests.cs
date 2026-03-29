using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Verifies consistency of the IntuneContentTypes static dictionaries.
/// </summary>
public class IntuneContentTypesTests
{
    [Fact]
    public void All_ContainsExpected20Types()
    {
        Assert.Equal(20, IntuneContentTypes.All.Count);
    }

    [Fact]
    public void GraphEndpoints_HasEntryForEveryType()
    {
        foreach (var type in IntuneContentTypes.All)
        {
            Assert.True(IntuneContentTypes.GraphEndpoints.ContainsKey(type),
                $"GraphEndpoints missing key: {type}");
        }
    }

    [Fact]
    public void FileNames_HasEntryForEveryType()
    {
        foreach (var type in IntuneContentTypes.All)
        {
            Assert.True(IntuneContentTypes.FileNames.ContainsKey(type),
                $"FileNames missing key: {type}");
        }
    }

    [Fact]
    public void FolderNames_HasEntryForEveryType()
    {
        foreach (var type in IntuneContentTypes.All)
        {
            Assert.True(IntuneContentTypes.FolderNames.ContainsKey(type),
                $"FolderNames missing key: {type}");
        }
    }

    [Fact]
    public void GraphEndpoints_AllStartWithDeviceManagementOrDeviceEnrollment()
    {
        foreach (var (type, endpoint) in IntuneContentTypes.GraphEndpoints)
        {
            Assert.True(
                endpoint.StartsWith("deviceManagement/", StringComparison.Ordinal) ||
                endpoint.StartsWith("deviceEnrollment/", StringComparison.Ordinal) ||
                endpoint.StartsWith("deviceAppManagement/", StringComparison.Ordinal) ||
                endpoint.StartsWith("identity/", StringComparison.Ordinal),
                $"Endpoint for {type} has unexpected prefix: {endpoint}");
        }
    }

    [Fact]
    public void FileNames_AllEndWithJson()
    {
        foreach (var (type, fileName) in IntuneContentTypes.FileNames)
        {
            Assert.EndsWith(".json", fileName);
        }
    }

    [Fact]
    public void FileNames_AllLowercase()
    {
        foreach (var (type, fileName) in IntuneContentTypes.FileNames)
        {
            Assert.Equal(fileName.ToLowerInvariant(), fileName);
        }
    }

    [Fact]
    public void FolderNames_AllNonEmpty()
    {
        foreach (var (type, folder) in IntuneContentTypes.FolderNames)
        {
            Assert.False(string.IsNullOrWhiteSpace(folder),
                $"FolderName for {type} is empty or whitespace");
        }
    }

    [Fact]
    public void DictionarySizes_AreAllEqual()
    {
        Assert.Equal(IntuneContentTypes.GraphEndpoints.Count, IntuneContentTypes.FileNames.Count);
        Assert.Equal(IntuneContentTypes.GraphEndpoints.Count, IntuneContentTypes.FolderNames.Count);
        Assert.Equal(IntuneContentTypes.GraphEndpoints.Count, IntuneContentTypes.All.Count);
    }

    [Theory]
    [InlineData(IntuneContentTypes.SettingsCatalog)]
    [InlineData(IntuneContentTypes.DeviceCompliancePolicy)]
    [InlineData(IntuneContentTypes.DeviceConfigurationPolicy)]
    [InlineData(IntuneContentTypes.WindowsDriverUpdate)]
    [InlineData(IntuneContentTypes.WindowsFeatureUpdate)]
    [InlineData(IntuneContentTypes.WindowsQualityUpdateProfile)]
    [InlineData(IntuneContentTypes.WindowsQualityUpdatePolicy)]
    [InlineData(IntuneContentTypes.PowerShellScript)]
    [InlineData(IntuneContentTypes.ProactiveRemediation)]
    [InlineData(IntuneContentTypes.MacOSShellScript)]
    [InlineData(IntuneContentTypes.WindowsAutoPilotProfile)]
    [InlineData(IntuneContentTypes.AppleBYODEnrollmentProfile)]
    [InlineData(IntuneContentTypes.AssignmentFilter)]
    public void EachContentType_PresentInAllDictionaries(string contentType)
    {
        Assert.Contains(contentType, IntuneContentTypes.All);
        Assert.True(IntuneContentTypes.GraphEndpoints.ContainsKey(contentType));
        Assert.True(IntuneContentTypes.FileNames.ContainsKey(contentType));
        Assert.True(IntuneContentTypes.FolderNames.ContainsKey(contentType));
    }

    [Fact]
    public void Dictionaries_AreCaseInsensitive()
    {
        // The dictionaries use StringComparer.OrdinalIgnoreCase
        Assert.True(IntuneContentTypes.GraphEndpoints.ContainsKey("settingscatalog"));
        Assert.True(IntuneContentTypes.FileNames.ContainsKey("SETTINGSCATALOG"));
        Assert.True(IntuneContentTypes.FolderNames.ContainsKey("settingsCatalog"));
    }
}
