// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using FuzzySharp;
using NuGetUtility.LicenseValidator.FileLicense;
using NuGetUtility.LicenseValidator.FileLicense.Templates;

public record MatchResult(int Score, OssLicenseType Type);

namespace NuGetUtility.LicenseValidator.FileLicense
{
    public class FileLicenseValidator
    {
        public OssLicenseType Validate(string licenseText, int fuzzyThreshold = 90)
        {
            if (string.IsNullOrWhiteSpace(licenseText))
            {
                return OssLicenseType.Unknown;
            }

            var results = new List<MatchResult>();

            foreach (var licence in LicenseTemplates.Map)
            {
                int score = Fuzz.TokenSetRatio(licenseText, licence.Value);
                results.Add(new MatchResult(score, licence.Key));
            }

            var winner = results.Where(x => x.Score >= fuzzyThreshold).OrderByDescending(x => x.Score).FirstOrDefault();

            return winner?.Type ?? OssLicenseType.Unknown;
        }
    }
}
