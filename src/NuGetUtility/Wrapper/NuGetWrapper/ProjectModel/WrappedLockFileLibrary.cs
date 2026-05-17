// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedLockFileLibrary(LockFileLibrary library) : ILockFileLibrary
    {
        public string Type => library.Type;

        public string Name => library.Name;

        public INuGetVersion Version => new WrappedNuGetVersion(library.Version);
    }
}
