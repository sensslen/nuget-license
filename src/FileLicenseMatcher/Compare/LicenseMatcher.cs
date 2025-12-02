// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;

namespace FileLicenseMatcher.Compare
{
    public class LicenseMatcher : IFileLicenseMatcher
    {
        private readonly IFileSystem _fileSystem;
        private readonly IDictionary<string, string> _fileLicenseMap;

        public LicenseMatcher(IFileSystem fileSystem, IDictionary<string, string> fileLicenseMap)
        {
            _fileSystem = fileSystem;
            _fileLicenseMap = fileLicenseMap;
        }

        public string Match(string licenseText)
        {
            string[] licenseContent = licenseText.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
            foreach (KeyValuePair<string, string> kvp in _fileLicenseMap)
            {
                if (!_fileSystem.File.Exists(kvp.Key))
                {
                    continue;
                }
                IEnumerable<string> fileContent = _fileSystem.File.ReadAllText(kvp.Key).Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                if (licenseContent.SequenceEqual(fileContent))
                {
                    return kvp.Value;
                }
            }
            return string.Empty;
        }
    }
}
