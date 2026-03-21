// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetLicense.Test.Output.Helper
{
    public class NuGetVersion : INuGetVersion
    {
        private readonly string _version;

        public NuGetVersion(string version)
        {
            _version = version;
        }

        public int CompareTo(INuGetVersion? other) => throw new NotImplementedException();

        public override string ToString()
        {
            return _version;
        }
    }
}
