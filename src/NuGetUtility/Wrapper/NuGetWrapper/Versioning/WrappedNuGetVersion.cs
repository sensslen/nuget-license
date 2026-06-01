// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace NuGetUtility.Wrapper.NuGetWrapper.Versioning
{
    internal class WrappedNuGetVersion : INuGetVersion
    {
        private readonly NuGetVersion _version;

        public WrappedNuGetVersion(NuGetVersion version)
        {
            _version = version;
        }

        public WrappedNuGetVersion(string version)
        {
            _version = new NuGetVersion(version);
        }

        public override bool Equals(object? obj)
        {
            if (obj is WrappedNuGetVersion other)
            {
                return Equals(other);
            }

            return false;
        }

        private bool Equals(WrappedNuGetVersion other)
        {
            return _version.Equals(other._version);
        }

        public override int GetHashCode()
        {
            return _version.GetHashCode();
        }

        public override string ToString()
        {
            return _version.ToString();
        }

        public int CompareTo(object? obj)
        {
            if (obj is not WrappedNuGetVersion wrappedNuGetVersion)
            {
                throw new ArgumentException($"{nameof(obj)} must be of type {nameof(WrappedNuGetVersion)} to be comparable.");
            }
            return _version.CompareTo(wrappedNuGetVersion._version);
        }

        public int CompareTo(INuGetVersion? other) => _version.CompareTo((other as WrappedNuGetVersion)?._version);

        public NuGetVersion Unwrap()
        {
            return _version;
        }

        internal static bool TryParse(string stringVersion, [NotNullWhen(true)] out WrappedNuGetVersion? version)
        {
            if (NuGetVersion.TryParse(stringVersion, out NuGetVersion? internalVersion))
            {
                version = new WrappedNuGetVersion(internalVersion);
                return true;
            }
            version = null;
            return false;
        }

        public static bool operator ==(WrappedNuGetVersion? left, WrappedNuGetVersion? right)
        {
            if (left is null)
            {
                return right is null;
            }
            return right is not null && left.Equals(right);
        }
        public static bool operator !=(WrappedNuGetVersion? left, WrappedNuGetVersion? right)
        {
            return !(left == right);
        }
        public static bool operator >(WrappedNuGetVersion left, WrappedNuGetVersion right)
        {
            return left.CompareTo(right) > 0;
        }
        public static bool operator <(WrappedNuGetVersion left, WrappedNuGetVersion right)
        {
            return left.CompareTo(right) < 0;
        }
        public static bool operator >=(WrappedNuGetVersion left, WrappedNuGetVersion right)
        {
            return !(left < right);
        }
        public static bool operator <=(WrappedNuGetVersion left, WrappedNuGetVersion right)
        {
            return !(left > right);
        }
    }
}
