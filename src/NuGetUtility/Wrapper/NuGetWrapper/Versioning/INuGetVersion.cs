// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.NuGetWrapper.Versioning
{
    public interface INuGetVersion : IComparable<INuGetVersion>
    {
        // Declared non-nullable so callers reconstructing a NuGetVersion from the string do not have to
        // guard against the nullable object.ToString() the interface would otherwise expose.
        string ToString();
    }
}
