// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;

namespace FileLicenseMatcher.Compare
{
    public class LicenseMatcher(IFileSystem fileSystem, IDictionary<string, string> fileLicenseMap)
        : IFileLicenseMatcher
    {
        public string Match(string licenseText)
        {
            string[] licenseContent = licenseText.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
            foreach (KeyValuePair<string, string> kvp in fileLicenseMap)
            {
                if (!fileSystem.File.Exists(kvp.Key))
                {
                    continue;
                }
                IEnumerable<string> fileContent = fileSystem.File.ReadAllText(kvp.Key).Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                if (licenseContent.SequenceEqual(fileContent))
                {
                    return kvp.Value;
                }
            }
            return string.Empty;
        }
    }
}
