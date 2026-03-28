namespace IntuneMonitor.Models;

/// <summary>
/// Constants for supported Intune content types and their Microsoft Graph API paths.
/// </summary>
public static class IntuneContentTypes
{
    public const string SettingsCatalog = "SettingsCatalog";
    public const string DeviceCompliancePolicy = "DeviceCompliancePolicy";
    public const string DeviceConfigurationPolicy = "DeviceConfigurationPolicy";
    public const string WindowsDriverUpdate = "WindowsDriverUpdate";
    public const string WindowsFeatureUpdate = "WindowsFeatureUpdate";
    public const string WindowsQualityUpdateProfile = "WindowsQualityUpdateProfile";
    public const string WindowsQualityUpdatePolicy = "WindowsQualityUpdatePolicy";
    public const string PowerShellScript = "PowerShellScript";
    public const string ProactiveRemediation = "ProactiveRemediation";
    public const string MacOSShellScript = "MacOSShellScript";
    public const string WindowsAutoPilotProfile = "WindowsAutoPilotProfile";
    public const string AppleBYODEnrollmentProfile = "AppleBYODEnrollmentProfile";
    public const string AssignmentFilter = "AssignmentFilter";
    public const string ConditionalAccessPolicy = "ConditionalAccessPolicy";
    public const string AppProtectionPolicy = "AppProtectionPolicy";
    public const string AppConfigurationPolicy = "AppConfigurationPolicy";
    public const string EndpointSecurityPolicy = "EndpointSecurityPolicy";
    public const string EnrollmentRestriction = "EnrollmentRestriction";
    public const string RoleDefinition = "RoleDefinition";
    public const string NamedLocation = "NamedLocation";

    /// <summary>
    /// Maps each content type to its Microsoft Graph API endpoint path.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> GraphEndpoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { SettingsCatalog, "deviceManagement/configurationPolicies" },
            { DeviceCompliancePolicy, "deviceManagement/deviceCompliancePolicies" },
            { DeviceConfigurationPolicy, "deviceManagement/deviceConfigurations" },
            { WindowsDriverUpdate, "deviceManagement/windowsDriverUpdateProfiles" },
            { WindowsFeatureUpdate, "deviceManagement/windowsFeatureUpdateProfiles" },
            { WindowsQualityUpdateProfile, "deviceManagement/windowsQualityUpdateProfiles" },
            { WindowsQualityUpdatePolicy, "deviceManagement/windowsQualityUpdatePolicies" },
            { PowerShellScript, "deviceManagement/deviceManagementScripts" },
            { ProactiveRemediation, "deviceManagement/deviceHealthScripts" },
            { MacOSShellScript, "deviceManagement/deviceShellScripts" },
            { WindowsAutoPilotProfile, "deviceManagement/windowsAutopilotDeploymentProfiles" },
            { AppleBYODEnrollmentProfile, "deviceEnrollment/appleUserInitiatedEnrollmentProfiles" },
            { AssignmentFilter, "deviceManagement/assignmentFilters" },
            { ConditionalAccessPolicy, "identity/conditionalAccess/policies" },
            { AppProtectionPolicy, "deviceAppManagement/managedAppPolicies" },
            { AppConfigurationPolicy, "deviceAppManagement/mobileAppConfigurations" },
            { EndpointSecurityPolicy, "deviceManagement/intents" },
            { EnrollmentRestriction, "deviceManagement/deviceEnrollmentConfigurations" },
            { RoleDefinition, "deviceManagement/roleDefinitions" },
            { NamedLocation, "identity/conditionalAccess/namedLocations" },
        };

    /// <summary>
    /// Maps each content type to the JSON filename used for backup storage.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FileNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { SettingsCatalog, "settingscatalog.json" },
            { DeviceCompliancePolicy, "devicecompliance.json" },
            { DeviceConfigurationPolicy, "deviceconfiguration.json" },
            { WindowsDriverUpdate, "windowsdriverupdate.json" },
            { WindowsFeatureUpdate, "windowsfeatureupdate.json" },
            { WindowsQualityUpdateProfile, "windowsqualityupdateprofile.json" },
            { WindowsQualityUpdatePolicy, "windowsqualityupdatepolicy.json" },
            { PowerShellScript, "powershellscript.json" },
            { ProactiveRemediation, "proactiveremediation.json" },
            { MacOSShellScript, "macosshellscript.json" },
            { WindowsAutoPilotProfile, "windowsautopilot.json" },
            { AppleBYODEnrollmentProfile, "applebyodenrollment.json" },
            { AssignmentFilter, "assignmentfilter.json" },
            { ConditionalAccessPolicy, "conditionalaccesspolicy.json" },
            { AppProtectionPolicy, "appprotectionpolicy.json" },
            { AppConfigurationPolicy, "appconfigurationpolicy.json" },
            { EndpointSecurityPolicy, "endpointsecuritypolicy.json" },
            { EnrollmentRestriction, "enrollmentrestriction.json" },
            { RoleDefinition, "roledefinition.json" },
            { NamedLocation, "namedlocation.json" },
        };

    /// <summary>
    /// Maps each content type to the folder name used for per-item backup storage.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FolderNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { SettingsCatalog, "SettingsCatalog" },
            { DeviceCompliancePolicy, "DeviceCompliancePolicy" },
            { DeviceConfigurationPolicy, "DeviceConfigurationPolicy" },
            { WindowsDriverUpdate, "WindowsDriverUpdate" },
            { WindowsFeatureUpdate, "WindowsFeatureUpdate" },
            { WindowsQualityUpdateProfile, "WindowsQualityUpdateProfile" },
            { WindowsQualityUpdatePolicy, "WindowsQualityUpdatePolicy" },
            { PowerShellScript, "PowerShellScript" },
            { ProactiveRemediation, "ProactiveRemediation" },
            { MacOSShellScript, "MacOSShellScript" },
            { WindowsAutoPilotProfile, "WindowsAutoPilotProfile" },
            { AppleBYODEnrollmentProfile, "AppleBYODEnrollmentProfile" },
            { AssignmentFilter, "AssignmentFilter" },
            { ConditionalAccessPolicy, "ConditionalAccessPolicy" },
            { AppProtectionPolicy, "AppProtectionPolicy" },
            { AppConfigurationPolicy, "AppConfigurationPolicy" },
            { EndpointSecurityPolicy, "EndpointSecurityPolicy" },
            { EnrollmentRestriction, "EnrollmentRestriction" },
            { RoleDefinition, "RoleDefinition" },
            { NamedLocation, "NamedLocation" },
        };

    /// <summary>All supported content type names.</summary>
    public static readonly IReadOnlyList<string> All = GraphEndpoints.Keys.ToList();
}
