// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace FileLicenseMatcher
{
    public interface IFileLicenseMatcher
    {
        string Match(string licenseText);
    }
}
