// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetLicense.LicenseValidator.FileLicenses
{
    public interface IFileLicenseMatcher
    {
        /// <summary>
        /// Match the given license text to a set of known license texts. If the license text matches reasonably closely to a known license,
        /// the license identifier is returned. If no match is found, null is returned.
        /// </summary>
        /// <param name="licenseText">The license text to match</param>
        /// <returns>The license expression or null</returns>
        string? Match(string licenseText);
    }
}
