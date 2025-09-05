// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SpdxLicenseMatcher.Generator
{
    [Generator]
    public class SpdxLicenseMatcherGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValueProvider<IEnumerable<AdditionalText>> xmlFiles = context.AdditionalTextsProvider
                .Where(static file => file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .Collect()
                .Select((files, _) => files.AsEnumerable());

            context.RegisterSourceOutput(xmlFiles, (spc, files) =>
            {
                if (!files.Any())
                {
                    return;
                }

                var allLicenses = new List<(string spdxId, string pattern)>();

                foreach (AdditionalText? file in files)
                {
                    string? content = file.GetText(spc.CancellationToken)?.ToString();
                    if (IsNullOrWhiteSpace(content)) continue;

                    try
                    {
                        allLicenses.AddRange(CreatePatternsFromXml(content));
                    }
                    catch (Exception)
                    {
                        // Could report a diagnostic here. For simplicity, we'll skip failed files.
                    }
                }

                var methodNames = new List<string>();

                // 1. Generate a separate file for each license's regex.
                foreach ((string spdxId, string pattern) in allLicenses)
                {
                    string sanitizedId = SanitizeIdentifier(spdxId);
                    string methodName = $"GetLicense_{sanitizedId}";
                    methodNames.Add(methodName);

                    string individualSource = GenerateIndividualLicenseSource(spdxId, pattern, methodName);
                    spc.AddSource($"SpdxLicenseMatcher.{sanitizedId}.g.cs", SourceText.From(individualSource, Encoding.UTF8));
                }

                // 2. Generate the main aggregator file that calls all the individual methods.
                string mainSource = GenerateMainMatcherClassSource(methodNames);
                spc.AddSource("SpdxLicenseMatcher.Main.g.cs", SourceText.From(mainSource, Encoding.UTF8));
            });
        }

        #region XML Parsing & Regex Building Logic

        private static IEnumerable<(string SpdxId, string Pattern)> CreatePatternsFromXml(string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent);
            XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            XElement root = doc.Root ?? throw new InvalidOperationException("XML document is missing a root element.");

            var licenseElements = new List<XElement>();

            if (root.Name.LocalName == "SPDXLicenseCollection")
            {
                licenseElements.AddRange(root.Elements(ns + "license"));
            }
            else if (root.Name.LocalName == "license")
            {
                licenseElements.Add(root);
            }

            foreach (XElement license in licenseElements)
            {
                string licenseId = license.Attribute("licenseId")?.Value ?? throw new InvalidOperationException("A license is missing the 'licenseId' attribute.");
                XElement textElement = license.Element(ns + "text") ?? throw new InvalidOperationException($"License {licenseId} is missing a <text> element.");

                var patternBuilder = new StringBuilder();
                BuildPatternRecursive(textElement, patternBuilder, ns);

                string finalPattern = $"^\\s*{patternBuilder.ToString().TrimEnd()}\\s*$";
                yield return (licenseId, finalPattern);
            }
        }

        private static void BuildPatternRecursive(XElement element, StringBuilder patternBuilder, XNamespace ns)
        {
            foreach (XNode node in element.Nodes())
            {
                if (node is XText textNode)
                {
                    string cleanedText = Regex.Replace(textNode.Value, @"\s+", " ").Trim();
                    if (!string.IsNullOrEmpty(cleanedText))
                    {
                        patternBuilder.Append(Regex.Escape(cleanedText));
                        patternBuilder.Append(@"\s+");
                    }
                }
                else if (node is XElement childElement)
                {
                    switch (childElement.Name.LocalName)
                    {
                        case "optional":
                            patternBuilder.Append("(?:");
                            BuildPatternRecursive(childElement, patternBuilder, ns);
                            patternBuilder.Append(")?");
                            break;

                        case "var":
                        case "alt":
                            string matchPattern = childElement.Attribute("match")?.Value ?? ".+";
                            patternBuilder.Append($"(?:{matchPattern})");
                            break;

                        case "br":
                            patternBuilder.Append(@"\s+");
                            break;

                        case "copyrightText":
                            patternBuilder.Append(@".*");
                            break;

                        case "titleText":
                            patternBuilder.Append(@"(");
                            BuildPatternRecursive(childElement, patternBuilder, ns);
                            patternBuilder.Append(@")*");
                            break;

                        case "p":
                        case "list":
                        case "item":
                        default:
                            BuildPatternRecursive(childElement, patternBuilder, ns);
                            break;
                    }
                }
            }
        }
        #endregion

        #region Source Generation

        /// <summary>
        /// Generates the C# source for a single license's static method.
        /// </summary>
        private static string GenerateIndividualLicenseSource(string spdxId, string pattern, string methodName)
        {
            string escapedPattern = SymbolDisplay.FormatLiteral(pattern, true);
            var sb = new StringBuilder();
            sb.Append(@"// <auto-generated/>
#nullable enable
using System.Text.RegularExpressions;

static partial class SpdxLicenseMatcher
{
    private static (string SpdxId, Regex Matcher) ");
            sb.Append(methodName);
            sb.Append(@"()
    {
        return (""");
            sb.Append(spdxId);
            sb.Append(@""", new Regex(");
            sb.Append(escapedPattern);
            sb.Append(@", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled));
    }
}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the main matcher class that aggregates all the individual license methods.
        /// </summary>
        private static string GenerateMainMatcherClassSource(List<string> methodNames)
        {
            var sb = new StringBuilder();
            sb.Append(@"// <auto-generated/>
#nullable enable
using System.Text.RegularExpressions;

/// <summary>
/// This class is generated at compile-time by the SpdxLicenseMatcherGenerator.
/// It contains pre-compiled regular expressions for all SPDX licenses found during the build.
/// </summary>
internal static partial class SpdxLicenseMatcher
{
    public static (string SpdxId, Regex Matcher)[] AllLicenseMatchers { get; } =
    {
        ");

            sb.Append(string.Join(",\n        ", methodNames.Select(m => m + "()")));

            sb.Append(@"
    };
}");
            return sb.ToString();
        }

        /// <summary>
        /// Cleans a license ID string to be a valid C# identifier.
        /// </summary>
        private static string SanitizeIdentifier(string id)
        {
            return id.Replace("-", "_").Replace(".", "_").Replace("+", "plus");
        }


        private static bool IsNullOrWhiteSpace([NotNullWhen(false)] string? value) => string.IsNullOrWhiteSpace(value);
        #endregion
    }
}

