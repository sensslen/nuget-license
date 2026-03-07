// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.Frameworks
{
    public class NuGetFrameworkUtility : INuGetFrameworkUtility
    {
        public bool IsEquivalent(string requestedFramework, INuGetFramework targetFramework)
        {
            NuGetFramework expectedFramework = ParseFramework(requestedFramework);
            NuGetFramework actualFramework = ParseFramework(targetFramework.ToString() ?? string.Empty);
            if (!string.Equals(expectedFramework.DotNetFrameworkName, actualFramework.DotNetFrameworkName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!expectedFramework.HasPlatform)
            {
                return true;
            }

            if (!actualFramework.HasPlatform)
            {
                return false;
            }

            if (!string.Equals(expectedFramework.Platform, actualFramework.Platform, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (expectedFramework.PlatformVersion == new Version(0, 0, 0, 0))
            {
                return true;
            }

            return expectedFramework.PlatformVersion == actualFramework.PlatformVersion;
        }

        public string Normalize(string targetFramework)
        {
            NuGetFramework framework = ParseFramework(targetFramework);
            return framework.GetShortFolderName();
        }

        public string Normalize(INuGetFramework targetFramework)
        {
            string framework = targetFramework.ToString() ?? throw new NugetWrapperException("Failed to parse target framework from lock file target.");
            return Normalize(framework);
        }

        private static NuGetFramework ParseFramework(string framework)
        {
            string normalizedInput = NormalizeFrameworkText(framework);
            return NuGetFramework.Parse(normalizedInput);
        }

        private static string NormalizeFrameworkText(string framework)
        {
            if (string.IsNullOrWhiteSpace(framework))
            {
                throw new NugetWrapperException("Failed to parse target framework from lock file target.");
            }

            string trimmed = framework.Trim();
            int ridSeparatorIndex = trimmed.IndexOf('/');
            return ridSeparatorIndex >= 0 ? trimmed[..ridSeparatorIndex] : trimmed;
        }
    }
}
