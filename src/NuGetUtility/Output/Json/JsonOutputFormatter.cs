// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using NuGetUtility.LicenseValidator;
using NuGetUtility.Serialization;

namespace NuGetUtility.Output.Json
{
    public class JsonOutputFormatter : IOutputFormatter
    {
        private readonly bool _printErrorsOnly;
        private readonly bool _skipIgnoredPackages;
        private readonly JsonSerializerOptions _options;
        private readonly HashSet<OutputColumnType>? _ignoredColumns;


        public JsonOutputFormatter(bool prettyPrint, bool printErrorsOnly, bool skipIgnoredPackages, IEnumerable<OutputColumnType>? ignoredColumns = null)
        {
            _printErrorsOnly = printErrorsOnly;
            _skipIgnoredPackages = skipIgnoredPackages;
            _options = new JsonSerializerOptions
            {
                Converters = { new NuGetVersionJsonConverter(), new ValidatedLicenseJsonConverterWithOmittingEmptyErrorList() },
                WriteIndented = prettyPrint
            };
            _ignoredColumns = ignoredColumns?.ToHashSet();
        }

        public async Task Write(Stream stream, IList<LicenseValidationResult> results)
        {
            if (_printErrorsOnly)
            {
                results = results.Where(r => r.ValidationErrors.Any()).ToList();
            }
            else if (_skipIgnoredPackages)
            {
                results = results.Where(r => r.LicenseInformationOrigin != LicenseInformationOrigin.Ignored).ToList();
            }

            var resultType = typeof(LicenseValidationResult);
            var props = resultType.GetProperties();
            Dictionary<OutputColumnType, PropertyInfo> validColumns = new();

            // Parse LicenseValidationResult properties to store the non-ignored columns
            foreach (var field in props)
            {
                if (!Enum.TryParse(field.Name, out OutputColumnType colType))
                {
                    continue;
                }

                if (_ignoredColumns?.Contains(colType) ?? false)
                {
                    continue;
                }

                validColumns.Add(colType, field); 
            }

            // Create the new array of dictionaries with only the non-ignored columns
            var dictionaries = results.Select(result =>
            {
                var dictionary = new Dictionary<string, object>();


                foreach (var field in validColumns)
                {
                    object? value = field.Value.GetValue(result);

                    switch (value)
                    {
                        case null:
                        case IList { Count: 0 }:
                            continue;
                        default:
                            dictionary.Add(field.Value.Name, value);
                            break;
                    }
                }

                return dictionary;
            });
            

            await JsonSerializer.SerializeAsync(stream, dictionaries, _options);
        }
    }
}
