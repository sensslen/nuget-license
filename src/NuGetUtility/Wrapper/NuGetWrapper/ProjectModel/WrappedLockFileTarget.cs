// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedLockFileTarget(LockFileTarget target) : ILockFileTarget
    {
        public INuGetFramework TargetFramework => new WrappedNuGetFramework(target.TargetFramework);

        public IEnumerable<ILockFileTargetLibrary> Libraries => target.Libraries.Select(l => new WrappedLockFileTargetLibrary(l));
    }
}
