// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.Frameworks
{
    public interface INuGetFrameworkUtility
    {
        bool IsEquivalent(string requestedFramework, INuGetFramework targetFramework);
        string Normalize(string targetFramework);
        string Normalize(INuGetFramework targetFramework);
    }
}
