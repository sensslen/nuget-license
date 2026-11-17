// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.Json;
using NuGetUtility.Serialization;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Test.Serialization
{
    public class NuGetVersionJsonConverterTest
    {
        private static readonly JsonSerializerOptions s_options = new() { Converters = { new NuGetVersionJsonConverter() } };

        [Test]
        public async Task Deserialize_Should_ReturnVersion_If_ValueIsValidVersionString()
        {
            INuGetVersion? result = JsonSerializer.Deserialize<INuGetVersion>("\"1.2.3-beta\"", s_options);

            await Assert.That(result?.ToString()).IsEqualTo("1.2.3-beta");
        }

        [Test]
        [Arguments("123")]
        [Arguments("{}")]
        public async Task Deserialize_Should_Throw_If_ValueIsNotString(string json)
        {
            JsonException? exception = await Assert.That(() => JsonSerializer.Deserialize<INuGetVersion>(json, s_options))
                .Throws<JsonException>();

            await Assert.That(exception!.Message).IsEqualTo("NuGet version needs to be serialized as a string.");
        }

        [Test]
        public async Task Deserialize_Should_Throw_If_ValueIsNotValidVersion()
        {
            JsonException? exception = await Assert.That(() => JsonSerializer.Deserialize<INuGetVersion>("\"not-a-version\"", s_options))
                .Throws<JsonException>();

            await Assert.That(exception!.Message).IsEqualTo("'not-a-version' is not a valid NuGet version.");
        }
    }
}
