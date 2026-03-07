// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public interface IAssetsPackageDependencyReader
    {
        Dictionary<string, HashSet<string>> GetPackageDependenciesForTargetFramework(string assetsPath, string normalizedTargetFramework);
    }
}
