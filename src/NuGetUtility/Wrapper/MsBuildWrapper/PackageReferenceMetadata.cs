// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    public record PackageReferenceMetadata(string PackageName, IReadOnlyDictionary<string, string> Metadata);
}
