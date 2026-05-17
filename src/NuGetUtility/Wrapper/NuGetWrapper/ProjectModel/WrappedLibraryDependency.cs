// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.LibraryModel;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedLibraryDependency(LibraryDependency dependency) : ILibraryDependency
    {
        public string Name => dependency.Name;
    }
}
