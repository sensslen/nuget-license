// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedPackageSpec(PackageSpec? spec) : IPackageSpec
    {
        public bool IsValid()
        {
            return spec != null;
        }

        public IEnumerable<ITargetFrameworkInformation> TargetFrameworks =>
            spec?.TargetFrameworks.Select(t => new WrappedTargetFrameworkInformation(t)) ??
            Enumerable.Empty<ITargetFrameworkInformation>();
    }
}
