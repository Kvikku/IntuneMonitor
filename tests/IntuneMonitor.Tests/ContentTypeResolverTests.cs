using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

public class ContentTypeResolverTests
{
    [Fact]
    public void Resolve_SpecifiedNonEmpty_ReturnsSpecified()
    {
        var specified = new[] { "SettingsCatalog", "DeviceCompliancePolicy" };

        var result = ContentTypeResolver.Resolve(specified, null);

        Assert.Equal(2, result.Count);
        Assert.Equal("SettingsCatalog", result[0]);
        Assert.Equal("DeviceCompliancePolicy", result[1]);
    }

    [Fact]
    public void Resolve_SpecifiedEmpty_FallsToConfig()
    {
        var specified = Enumerable.Empty<string>();
        var config = new List<string> { "PowerShellScript" };

        var result = ContentTypeResolver.Resolve(specified, config);

        Assert.Single(result);
        Assert.Equal("PowerShellScript", result[0]);
    }

    [Fact]
    public void Resolve_SpecifiedNull_FallsToConfig()
    {
        var config = new List<string> { "AssignmentFilter", "MacOSShellScript" };

        var result = ContentTypeResolver.Resolve(null, config);

        Assert.Equal(2, result.Count);
        Assert.Equal("AssignmentFilter", result[0]);
    }

    [Fact]
    public void Resolve_SpecifiedNull_ConfigEmpty_FallsToAll()
    {
        var result = ContentTypeResolver.Resolve(null, new List<string>());

        Assert.Equal(IntuneContentTypes.All.Count, result.Count);
    }

    [Fact]
    public void Resolve_SpecifiedNull_ConfigNull_FallsToAll()
    {
        var result = ContentTypeResolver.Resolve(null, null);

        Assert.Equal(IntuneContentTypes.All.Count, result.Count);
    }

    [Fact]
    public void Resolve_SpecifiedTakesPrecedenceOverConfig()
    {
        var specified = new[] { "SettingsCatalog" };
        var config = new List<string> { "PowerShellScript", "MacOSShellScript" };

        var result = ContentTypeResolver.Resolve(specified, config);

        Assert.Single(result);
        Assert.Equal("SettingsCatalog", result[0]);
    }

    [Fact]
    public void Resolve_ReturnsNewListInstance()
    {
        var specified = new[] { "SettingsCatalog" };

        var result1 = ContentTypeResolver.Resolve(specified, null);
        var result2 = ContentTypeResolver.Resolve(specified, null);

        Assert.NotSame(result1, result2);
    }
}
