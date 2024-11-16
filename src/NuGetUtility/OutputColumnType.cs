// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.ComponentModel;

namespace NuGetUtility;

public enum OutputColumnType
{
    [Description("Package")]
    PackageId,
    [Description("Version")]
    PackageVersion,
    [Description("License Information Origin")]
    LicenseInformationOrigin,
    [Description("License Expression")]
    License, // License Expression 
    [Description("License Url")]
    LicenseUrl,
    [Description("Copyright")]
    Copyright,
    [Description("Authors")]
    Authors,
    [Description("Package Project Url")]
    PackageProjectUrl,
    [Description("Error")]
    ValidationErrors,
    [Description("Error Context")]
    ErrorContext,
}
