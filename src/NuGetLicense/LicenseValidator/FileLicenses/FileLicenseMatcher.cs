// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using FuzzySharp;

namespace NuGetLicense.LicenseValidator.FileLicenses
{
    public class FileLicenseMatcher : IFileLicenseMatcher
    {
        public const int MATCH_THRESHOLD = 95;
        private readonly IImmutableDictionary<string, string> _knownLicenses;

        public FileLicenseMatcher(IImmutableDictionary<string, string> knownLicenses)
        {
            _knownLicenses = knownLicenses;
        }

        public string? Match(string licenseText)
        {
            IEnumerable<(int Score, string LicenseExpression)> scoredMatches = _knownLicenses.Select(pair => (Score: Fuzz.TokenDifferenceRatio(licenseText, pair.Value), LicenseExpression: pair.Key));
#if NETFRAMEWORK
            (int Score, string LicenseExpression) bestMatch = scoredMatches.OrderByDescending(t => t.Score).First();
#else
            (int Score, string LicenseExpression) bestMatch = scoredMatches.MaxBy(t => t.Score);
#endif
            if (bestMatch.Score >= 90)
            {
                return bestMatch.LicenseExpression;
            }
            return null;
        }
    }
}
