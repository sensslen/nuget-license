// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;
using NuGetUtility.Wrapper.NuGetWrapper.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedTargetFrameworkInformation(TargetFrameworkInformation info) : ITargetFrameworkInformation
    {
        public INuGetFramework FrameworkName => new WrappedNuGetFramework(info.FrameworkName);

        public IEnumerable<ILibraryDependency> Dependencies => info.Dependencies.Select(library => new WrappedLibraryDependency(library));

        public override string ToString()
        {
            return info.ToString();
        }
    }
}
