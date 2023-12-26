using NuGetUtility.LicenseValidator;

namespace NuGetUtility.Test.LicenseValidator
{
    public class LicenseValidationResultValueEqualityComparer : IEqualityComparer<LicenseValidationResult>
    {
        public bool Equals(LicenseValidationResult? x, LicenseValidationResult? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.ValidationChecks.SequenceEqual(y.ValidationChecks) && (x.License == y.License) &&
                   (x.LicenseInformationOrigin == y.LicenseInformationOrigin) && (x.PackageId == y.PackageId) &&
                   x.PackageVersion.Equals(y.PackageVersion) && (x.PackageProjectUrl == y.PackageProjectUrl);
        }
        public int GetHashCode(LicenseValidationResult obj)
        {
            return HashCode.Combine(GetHashCode(obj.ValidationChecks),
                obj.License,
                (int)obj.LicenseInformationOrigin,
                obj.PackageId,
                obj.PackageVersion,
                obj.PackageProjectUrl);
        }
        private HashCode GetHashCode(List<ValidationCheck> validationChecks)
        {
            var code = new HashCode();
            foreach (ValidationCheck check in validationChecks)
            {
                code.Add(check);
            }
            return code;
        }
    }
}
