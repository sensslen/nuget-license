﻿// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.ProjectModel;

namespace NuGetUtility.Wrapper.NuGetWrapper.ProjectModel
{
    internal class WrappedLockFile : ILockFile
    {
        private readonly LockFile _file;

        public WrappedLockFile(LockFile file)
        {
            _file = file;
        }

        public IEnumerable<ILockFileLibrary> Libraries => _file.Libraries.Select(l => new WrappedLockFileLibrary(l));

        public IPackageSpec PackageSpec => new WrappedPackageSpec(_file.PackageSpec);
        public IEnumerable<ILockFileTarget>? Targets => _file.Targets?.Select(t => new WrappedLockFileTarget(t));

        public bool TryGetErrors(out string[] errors)
        {
            IAssetsLogMessage[] fileErrors = _file.LogMessages.Where(l => l.Level == NuGet.Common.LogLevel.Error).ToArray();
            errors = fileErrors.Select(e => $"[{e.Code}] {e.Message}").ToArray();
            return errors.Length > 0;
        }
    }
}
