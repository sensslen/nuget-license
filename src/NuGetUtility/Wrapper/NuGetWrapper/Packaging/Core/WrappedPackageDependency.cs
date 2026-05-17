// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Packaging.Core;

namespace NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core
{
    internal class WrappedPackageDependency(PackageDependency dependency) : IPackageDependency
    {
        public string Id { get; } = dependency.Id;
    }
}

