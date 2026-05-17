// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;

namespace FileLicenseMatcher.Combine
{
    public class LicenseMatcher(IReadOnlyList<IFileLicenseMatcher> matchers) : IFileLicenseMatcher
    {
        private readonly IReadOnlyCollection<IFileLicenseMatcher> _matchers = matchers;

        public string Match(string licenseText)
        {
            foreach (IFileLicenseMatcher matcher in _matchers)
            {
                string result = matcher.Match(licenseText);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            return string.Empty;
        }
    }
}
