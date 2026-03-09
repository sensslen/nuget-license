// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public interface ILockFileTargetLibrary : ILockFileLibrary
    {
        IReadOnlyList<IPackageDependency> Dependencies { get; }
    }
}
