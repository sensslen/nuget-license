// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public interface IPackageSpec
    {
        public IEnumerable<ITargetFrameworkInformation> TargetFrameworks { get; }
        bool IsValid();
    }
}
