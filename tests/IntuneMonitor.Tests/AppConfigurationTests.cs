using IntuneMonitor.Config;

namespace IntuneMonitor.Tests;

/// <summary>
/// Verifies that configuration POCO defaults are correct.
/// </summary>
public class AppConfigurationTests
{
    [Fact]
    public void AppConfiguration_DefaultsAreValid()
    {
        var config = new AppConfiguration();

        Assert.NotNull(config.Authentication);
        Assert.NotNull(config.Backup);
        Assert.NotNull(config.Monitor);
        Assert.NotNull(config.ContentTypes);
        Assert.Empty(config.ContentTypes);
    }

    [Fact]
    public void AuthenticationConfig_Defaults()
    {
        var auth = new AuthenticationConfig();

        Assert.Equal(string.Empty, auth.TenantId);
        Assert.Equal(string.Empty, auth.ClientId);
        Assert.Equal("ClientSecret", auth.Method);
        Assert.Null(auth.ClientSecret);
        Assert.Null(auth.CertificatePath);
        Assert.Null(auth.CertificatePassword);
        Assert.Null(auth.CertificateThumbprint);
    }

    [Fact]
    public void BackupConfig_Defaults()
    {
        var backup = new BackupConfig();

        Assert.Equal("LocalFile", backup.StorageType);
        Assert.Equal("./intune-backup", backup.Path);
        Assert.Equal(string.Empty, backup.SubDirectory);
        Assert.Null(backup.GitRemoteUrl);
        Assert.Equal("main", backup.GitBranch);
        Assert.Null(backup.GitUsername);
        Assert.Null(backup.GitToken);
        Assert.Equal("IntuneMonitor", backup.GitAuthorName);
        Assert.Equal("intune-monitor@noreply.local", backup.GitAuthorEmail);
        Assert.True(backup.AutoCommit);
        Assert.Null(backup.HtmlExportReportPath);
        Assert.True(backup.OpenHtmlExportReport);
    }

    [Fact]
    public void MonitorConfig_Defaults()
    {
        var monitor = new MonitorConfig();

        Assert.Equal(0, monitor.IntervalMinutes);
        Assert.False(monitor.ChangesOnly);
        Assert.Null(monitor.ReportOutputPath);
        Assert.Equal("Info", monitor.MinSeverity);
        Assert.Null(monitor.HtmlReportOutputPath);
        Assert.True(monitor.OpenHtmlReport);
    }
}
