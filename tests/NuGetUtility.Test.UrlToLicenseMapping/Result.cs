// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;

namespace NuGetUtility.Test.LicenseValidator
{
    public readonly struct Result<T>
    {
        public T Value { get; init; }
        public string? Error { get; init; }
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess => Error is null;
    }
}
