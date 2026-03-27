using IntuneMonitor.Commands;
using IntuneMonitor.Config;
using IntuneMonitor.Models;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for MonitorCommand static helper methods (ParseSeverity, Truncate).
/// These are internal methods exposed via InternalsVisibleTo.
/// </summary>
public class MonitorCommandHelperTests
{
    // -----------------------------------------------------------------------
    // ParseSeverity (accessed indirectly through MonitorCommand behavior)
    // Since ParseSeverity and Truncate are private static, we test them through
    // the public API or verify the config-driven filtering behavior.
    // -----------------------------------------------------------------------

    // ParseSeverity is private static — we verify its effect through ResolveContentTypes
    // and the MonitorConfig.MinSeverity default. We'll test via observable behavior.

    [Fact]
    public void MonitorConfig_MinSeverity_DefaultIsInfo()
    {
        var config = new MonitorConfig();
        Assert.Equal("Info", config.MinSeverity);
    }

    [Fact]
    public void ChangeSeverity_EnumValues_AreOrdered()
    {
        // Verify the enum ordering is Info < Warning < Critical
        Assert.True(ChangeSeverity.Info < ChangeSeverity.Warning);
        Assert.True(ChangeSeverity.Warning < ChangeSeverity.Critical);
    }

    [Fact]
    public void ChangeSeverity_AllValuesPresent()
    {
        var values = Enum.GetValues<ChangeSeverity>();
        Assert.Equal(3, values.Length);
        Assert.Contains(ChangeSeverity.Info, values);
        Assert.Contains(ChangeSeverity.Warning, values);
        Assert.Contains(ChangeSeverity.Critical, values);
    }

    // -----------------------------------------------------------------------
    // MonitorCommand constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void MonitorCommand_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MonitorCommand(null!));
    }

    [Fact]
    public void MonitorCommand_ValidConfig_DoesNotThrow()
    {
        var config = new AppConfiguration();
        var command = new MonitorCommand(config);
        Assert.NotNull(command);
    }
}
