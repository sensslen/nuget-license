// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetLicense.Test.Output.Helper
{
    public class NuGetVersion(string version) : INuGetVersion
    {
        public int CompareTo(INuGetVersion? other) => throw new NotImplementedException();

        public override string ToString()
        {
            return version;
        }
    }
}
