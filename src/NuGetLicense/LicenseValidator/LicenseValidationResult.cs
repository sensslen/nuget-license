// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetLicense.LicenseValidator
{
    public record LicenseValidationResult(string PackageId,
        INuGetVersion PackageVersion,
        string? PackageProjectUrl,
        string? License,
        string? LicenseUrl,
        string? Copyright,
        string? Authors,
        LicenseInformationOrigin LicenseInformationOrigin,
        List<ValidationError>? ValidationErrors = null)
    {
        public List<ValidationError> ValidationErrors { get; } = ValidationErrors ?? new List<ValidationError>();

        public string? License { get; set; } = License;
        public string? LicenseUrl { get; set; } = LicenseUrl;
        public LicenseInformationOrigin LicenseInformationOrigin { get; set; } = LicenseInformationOrigin;

        public override string ToString() => $"{nameof(LicenseValidationResult)} {{{nameof(PackageId)} = {PackageId}, " +
                                                                                 $"{nameof(PackageVersion)} = {PackageVersion}, " +
                                                                                 $"{nameof(PackageProjectUrl)} = {PackageProjectUrl}, " +
                                                                                 $"{nameof(Copyright)} = {Copyright}, " +
                                                                                 $"{nameof(Authors)} = {Authors}, " +
                                                                                 $"{nameof(ValidationErrors)} = [{string.Join(",", ValidationErrors)}], " +
                                                                                 $"{nameof(License)} = {License}, " +
                                                                                 $"{nameof(LicenseUrl)} = {LicenseUrl}, " +
                                                                                 $"{nameof(LicenseInformationOrigin)} = {LicenseInformationOrigin}}}";
    }
}
