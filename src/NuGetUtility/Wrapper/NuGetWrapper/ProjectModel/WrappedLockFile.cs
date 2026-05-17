// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedLockFile(LockFile file) : ILockFile
    {
        public IPackageSpec PackageSpec => new WrappedPackageSpec(file.PackageSpec);
        public IEnumerable<ILockFileTarget> Targets => file.Targets.Select(t => new WrappedLockFileTarget(t));
        public string Path => file.Path;

        public bool TryGetErrors(out string[] errors)
        {
            IAssetsLogMessage[] fileErrors = file.LogMessages.Where(l => l.Level == NuGet.Common.LogLevel.Error).ToArray();
            errors = fileErrors.Select(e => $"[{e.Code}] {e.Message}").ToArray();
            return errors.Length > 0;
        }
    }
}
