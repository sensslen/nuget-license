// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    public interface ILockFile
    {
        bool TryGetErrors(out string[] errors);
        IPackageSpec PackageSpec { get; }
        IEnumerable<ILockFileTarget> Targets { get; }
        string Path { get; }
    }
}
