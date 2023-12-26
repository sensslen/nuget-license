using System.Text.Json;
using System.Text.Json.Serialization;
using NuGetUtility.LicenseValidator;

namespace NuGetUtility.Serialization
{
    internal class PackageValidationContextErrorOnlyConverter : JsonConverter<LicenseValidationResult>
    {
        public override LicenseValidationResult? Read(ref Utf8JsonReader reader,
                    Type typeToConvert,
                    JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        public override void Write(Utf8JsonWriter writer, LicenseValidationResult value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (System.Reflection.PropertyInfo propertyInfo in value.GetType().GetProperties())
            {
                switch (propertyInfo.Name)
                {
                    case nameof(value.ValidationChecks):
                        if (value.ValidationChecks.Exists(c => c.Error is not null))
                        {
                            writer.WritePropertyName(propertyInfo.Name);
                            JsonSerializer.Serialize(writer, value.ValidationChecks.Where(c => c.Error is not null), options);
                        }
                        break;
                    default:
                        object? writeValue = propertyInfo.GetValue(value);
                        if (writeValue != null)
                        {
                            writer.WritePropertyName(propertyInfo.Name);
                            JsonSerializer.Serialize(writer, propertyInfo.GetValue(value), options);
                        }
                        break;
                }
            }
            writer.WriteEndObject();
        }
    }
}
