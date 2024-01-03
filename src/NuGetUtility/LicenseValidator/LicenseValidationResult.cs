using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.LicenseValidator
{
    public record LicenseValidationResult(string PackageId,
        INuGetVersion PackageVersion,
        string? PackageProjectUrl,
        string? License,
        string? Copyright,
        IEnumerable<string>? Authors,
        LicenseInformationOrigin LicenseInformationOrigin,
        List<ValidationError>? ValidationErrors = null)
    {
        public List<ValidationError> ValidationErrors { get; } = ValidationErrors ?? new List<ValidationError>();

        public string? License { get; set; } = License;
        public LicenseInformationOrigin LicenseInformationOrigin { get; set; } = LicenseInformationOrigin;
    }
}
