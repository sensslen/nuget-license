// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using FuzzySharp;

namespace NuGetLicense.LicenseValidator.FileLicense;

internal record MatchResult(int Score, string Type);

public static class FileLicenseMatcher
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
