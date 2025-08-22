// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using FuzzySharp;
using NuGetLicense.LicenseValidator.FileLicense;

public record MatchResult(int Score, string Type);

namespace NuGetUtility.LicenseValidator.FileLicense
{
    public class FileLicenseMatcher
    {
        public static string? FindBestMatch(string licenseText, int fuzzyThreshold = 90)
        {
            if (string.IsNullOrWhiteSpace(licenseText))
            {
                return null;
            }

            var results = new List<MatchResult>();

            foreach (KeyValuePair<string, string> licence in FileLicenseMap.Map)
            {
                int score = Fuzz.TokenDifferenceRatio(licenseText, licence.Value);
                results.Add(new MatchResult(score, licence.Key));
            }

            MatchResult? winner = results.Where(x => x.Score >= fuzzyThreshold).OrderByDescending(x => x.Score).FirstOrDefault();
            return winner?.Type;
        }
    }
}
