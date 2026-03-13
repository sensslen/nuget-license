// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGet.Frameworks;

namespace NuGetUtility.Wrapper.NuGetWrapper.Frameworks
{
    internal class WrappedNuGetFramework : INuGetFramework
    {
        private readonly NuGetFramework _framework;

        public WrappedNuGetFramework(NuGetFramework framework)
        {
            _framework = framework;
        }

        public override bool Equals(object? obj)
        {
            if (obj is WrappedNuGetFramework wrapped)
            {
                return _framework.Equals(wrapped._framework);
            }

            return false;
        }

        public bool Equals(string targetFramework)
        {
            var other = NuGetFramework.Parse(targetFramework);
            if (_framework.DotNetFrameworkName != other.DotNetFrameworkName)
            {
                return false;
            }

            if (!other.HasPlatform)
            {
                return true;
            }

            if (!_framework.HasPlatform)
            {
                return false;
            }

            if (other.Platform != _framework.Platform)
            {
                return false;
            }

            if (other.PlatformVersion == new Version(0, 0, 0, 0))
            {
                return true;
            }

            return _framework.PlatformVersion == other.PlatformVersion;
        }

        public override string ToString()
        {
            return _framework.ToString();
        }

        public override int GetHashCode()
        {
            return _framework.GetHashCode();
        }
    }
}
