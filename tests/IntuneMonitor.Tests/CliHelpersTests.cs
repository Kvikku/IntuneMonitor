using IntuneMonitor.Commands;
using IntuneMonitor.Config;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for CliHelpers — the extracted helper methods from Program.cs.
/// </summary>
public class CliHelpersTests
{
    [Theory]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Trace)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Debug)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Information)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Warning)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Error)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Critical)]
    public void CreateLoggerFactory_ReturnsNonNull(Microsoft.Extensions.Logging.LogLevel level)
    {
        using var factory = CliHelpers.CreateLoggerFactory(level);
        Assert.NotNull(factory);
    }

    [Fact]
    public void CreateLoggerFactory_CreatesWorkingLogger()
    {
        using var factory = CliHelpers.CreateLoggerFactory(Microsoft.Extensions.Logging.LogLevel.Information);
        var logger = factory.CreateLogger("Test");
        Assert.NotNull(logger);
    }
}
