// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Serialization
{
    public class NuGetVersionJsonConverter : JsonConverter<INuGetVersion>
    {
        public override INuGetVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("NuGet version needs to be serialized as a string.");
            }

            // we already know that the token is a string so we can safely call GetString() without checking for null
            string stringVersion = reader.GetString()!;
            if (WrappedNuGetVersion.TryParse(stringVersion, out WrappedNuGetVersion? version))
            {
                return version;
            }

            throw new JsonException($"'{stringVersion}' is not a valid NuGet version.");
        }

        public override void Write(Utf8JsonWriter writer, INuGetVersion value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(INuGetVersion).IsAssignableFrom(typeToConvert);
        }
    }
}
