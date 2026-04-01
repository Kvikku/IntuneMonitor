using IntuneMonitor.UI;

namespace IntuneMonitor.Tests;

/// <summary>
/// Tests for MenuConstants — verifies all menu strings are defined and consistent.
/// </summary>
public class MenuConstantsTests
{
    [Fact]
    public void MainMenuChoices_ContainsAllExpectedOptions()
    {
        Assert.Equal(11, MenuConstants.MainMenuChoices.Length);
    }

    [Fact]
    public void MainMenuChoices_IncludesExportPolicies()
    {
        Assert.Contains(MenuConstants.ExportPolicies, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesImportPolicies()
    {
        Assert.Contains(MenuConstants.ImportPolicies, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesMonitorForChanges()
    {
        Assert.Contains(MenuConstants.MonitorForChanges, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesRollbackDrift()
    {
        Assert.Contains(MenuConstants.RollbackDrift, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesCompareBackups()
    {
        Assert.Contains(MenuConstants.CompareBackups, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesAnalyzeDependencies()
    {
        Assert.Contains(MenuConstants.AnalyzeDependencies, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesValidateBackups()
    {
        Assert.Contains(MenuConstants.ValidateBackups, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesReviewAuditLogs()
    {
        Assert.Contains(MenuConstants.ReviewAuditLogs, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesListContentTypes()
    {
        Assert.Contains(MenuConstants.ListContentTypes, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesSettingsOverview()
    {
        Assert.Contains(MenuConstants.SettingsOverview, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_IncludesExit()
    {
        Assert.Contains(MenuConstants.Exit, MenuConstants.MainMenuChoices);
    }

    [Fact]
    public void MainMenuChoices_ExitIsLast()
    {
        Assert.Equal(MenuConstants.Exit, MenuConstants.MainMenuChoices[^1]);
    }

    [Fact]
    public void MainMenuChoices_HasNoDuplicates()
    {
        var distinct = MenuConstants.MainMenuChoices.Distinct().Count();
        Assert.Equal(MenuConstants.MainMenuChoices.Length, distinct);
    }

    [Fact]
    public void MainMenuTitle_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(MenuConstants.MainMenuTitle));
    }

    [Fact]
    public void DryRunPrompt_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(MenuConstants.DryRunPrompt));
    }

    [Fact]
    public void ContentTypeFilterPrompt_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(MenuConstants.ContentTypeFilterPrompt));
    }

    [Theory]
    [InlineData(nameof(MenuConstants.ExportPolicies))]
    [InlineData(nameof(MenuConstants.ImportPolicies))]
    [InlineData(nameof(MenuConstants.MonitorForChanges))]
    [InlineData(nameof(MenuConstants.RollbackDrift))]
    [InlineData(nameof(MenuConstants.CompareBackups))]
    [InlineData(nameof(MenuConstants.Exit))]
    public void MenuConstant_IsNotNullOrWhitespace(string fieldName)
    {
        var field = typeof(MenuConstants).GetField(fieldName);
        Assert.NotNull(field);
        var value = field.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));
    }
}
