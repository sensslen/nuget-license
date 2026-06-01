// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetLicense
{
    /// <summary>
    /// Options for reading referenced packages from projects.
    /// </summary>
    public class PackageReaderOptions
    {
        /// <summary>
        /// True to include transitive dependencies; otherwise, false.
        /// </summary>
        public bool IncludeTransitive { get; set; } = false;

        /// <summary>
        /// Target framework moniker to evaluate. If null, all available target frameworks are evaluated.
        /// </summary>
        public string? TargetFramework { get; set; } = null;

        /// <summary>
        /// True to exclude packages with Publish="false" metadata. When transitive dependencies are included,
        /// packages reachable only through those excluded roots are also excluded.
        /// </summary>
        public bool ExcludePublishFalse { get; set; } = false;

        /// <summary>
        /// True to exclude packages with PrivateAssets="all" metadata (development-only packages).
        /// When transitive dependencies are included, packages reachable only through those excluded roots are also excluded.
        /// </summary>
        public bool ExcludePrivateAssets { get; set; } = false;

        /// <summary>
        /// True to include shared projects; otherwise, false.
        /// </summary>
        public bool IncludeSharedProjects { get; set; } = false;
    }
}
