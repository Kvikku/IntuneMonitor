namespace IntuneMonitor.UI;

/// <summary>
/// Constants for interactive menu option strings.
/// Centralizes all menu text for easier maintenance and potential future localization.
/// </summary>
internal static class MenuConstants
{
    // Main menu choices
    public const string ExportPolicies = "Export policies";
    public const string ImportPolicies = "Import policies";
    public const string MonitorForChanges = "Monitor for changes";
    public const string RollbackDrift = "Rollback drift";
    public const string CompareBackups = "Compare backups (diff)";
    public const string AnalyzeDependencies = "Analyze dependencies";
    public const string ValidateBackups = "Validate backups";
    public const string ReviewAuditLogs = "Review audit logs";
    public const string ListContentTypes = "List content types";
    public const string SettingsOverview = "Settings overview";
    public const string Exit = "Exit";

    /// <summary>All main menu choices in display order.</summary>
    public static readonly string[] MainMenuChoices =
    {
        ExportPolicies,
        ImportPolicies,
        MonitorForChanges,
        RollbackDrift,
        CompareBackups,
        AnalyzeDependencies,
        ValidateBackups,
        ReviewAuditLogs,
        ListContentTypes,
        SettingsOverview,
        Exit
    };

    // Menu prompts
    public const string MainMenuTitle = "[bold dodgerblue1]What would you like to do?[/]";
    public const string ContentTypeFilterPrompt = "Limit to specific content types? (No = all types)";
    public const string ContentTypeSelectionTitle = "Select content types to include:";
    public const string DryRunPrompt = "Dry run (preview only, no changes)?";
    public const string GoodbyeMessage = "[dim]Goodbye![/]";
    public const string ScheduledMonitoringHint = "[dim]Press Ctrl+C to stop scheduled monitoring[/]";
}
