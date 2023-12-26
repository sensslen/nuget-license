using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.LicenseValidator
{
    public record LicenseValidationResult(string PackageId,
        INuGetVersion PackageVersion,
        string? PackageProjectUrl,
        string? License,
        LicenseInformationOrigin LicenseInformationOrigin,
        List<ValidationCheck> ValidationChecks)
    {
        public string? License { get; set; } = License;
        public LicenseInformationOrigin LicenseInformationOrigin { get; set; } = LicenseInformationOrigin;

        public override string ToString() => $"LicenseValidationResult {{ PackageId = {PackageId}, PackageVersion = {PackageVersion}, PackageProjectUrl = {PackageProjectUrl}, ValidationChecks = {{{string.Join(',', ValidationChecks)}}}, License = {License}, LicenseInformationOrigin = {LicenseInformationOrigin} }}";
    }
}
