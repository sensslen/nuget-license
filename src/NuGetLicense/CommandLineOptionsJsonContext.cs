// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.Json.Serialization;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Serialization;

namespace NuGetLicense;

[JsonSourceGenerationOptions(
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    Converters = [typeof(NuGetVersionJsonConverter)]
)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<Uri, string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(CustomPackageInformation[]))]
internal partial class CommandLineOptionsJsonContext : JsonSerializerContext;
