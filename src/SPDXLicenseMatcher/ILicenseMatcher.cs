// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher
{
    public interface ILicenseMatcher
    {
        string Match(string licenseText);
    }
}
