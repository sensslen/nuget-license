// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher.Test
{
    public class LicenseMatcherTest
    {
        public record Case(string Identifier, string Content);

        public class AllSpdxLicensesFastLicenseMatcher : ILicenseMatcher
        {
            private readonly FastLicenseMatcher _fastLicenseMatcher = new FastLicenseMatcher(Spdx.Licenses.SpdxLicenseStore.Licenses);
            public string Match(string licenseText) => _fastLicenseMatcher.Match(licenseText);
        }

        [ClassDataSource<AllSpdxLicensesFastLicenseMatcher>(Shared = SharedType.PerTestSession)]
        public required AllSpdxLicensesFastLicenseMatcher FastlicenseMatcher { get; init; }

#pragma warning disable S101 // Types should be named in PascalCase
        public static class SPDXLicensesTestSource
#pragma warning restore S101 // Types should be named in PascalCase
        {
            private const string PREFIX = "SPDXLicenseMatcher.Test.SPDXLicenses.";
            private static readonly int s_prefixLength = PREFIX.Length;
            private static readonly int s_postfixLength = ".txt".Length;
            public static IEnumerable<Func<Case>> GetCases()
            {
                var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string name in executingAssembly.GetManifestResourceNames().Where(n => n.StartsWith(PREFIX)).Where(n => n.EndsWith("txt")))
                {
                    string expectedIdentifier = name.Substring(s_prefixLength, name.Length - s_postfixLength - s_prefixLength);
                    using var reader = new StreamReader(executingAssembly.GetManifestResourceStream(name)!);
                    yield return () => new Case(expectedIdentifier, reader.ReadToEnd());
                }
            }
        }

#pragma warning disable S101 // Types should be named in PascalCase
        public static class NonSPDXLicensesTestSource
#pragma warning restore S101 // Types should be named in PascalCase
        {
            private const string PREFIX = "SPDXLicenseMatcher.Test.NonSpdxTestLicenses.";
            public static IEnumerable<Func<string>> GetCases()
            {
                var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string name in executingAssembly.GetManifestResourceNames().Where(n => n.StartsWith(PREFIX)).Where(n => n.EndsWith("txt")))
                {
                    using var reader = new StreamReader(executingAssembly.GetManifestResourceStream(name)!);
                    yield return () => reader.ReadToEnd();
                }
            }
        }

#pragma warning disable S125 // Sections of code should not be commented out
        /*
                [Test]
                [MethodDataSource(typeof(SPDXLicensesTestSource), nameof(SPDXLicensesTestSource.GetCases))]
                [Skip("these tests run very slow")]
                public async Task License_Matcher_Should_Pick_Correct_License(Case @case)
                {
                    var matcher = new LicenseMatcher();

                    await Assert.That(matcher.Match(@case.Content)).Contains(@case.Identifier);
                }
                */
#pragma warning restore S125 // Sections of code should not be commented out

        [Test]
        [MethodDataSource(typeof(SPDXLicensesTestSource), nameof(SPDXLicensesTestSource.GetCases))]
        public async Task Fast_License_Matcher_Should_Pick_Correct_License(Case @case)
        {
            await Assert.That(FastlicenseMatcher.Match(@case.Content)).Contains(@case.Identifier);
        }

        [Test]
        [MethodDataSource(typeof(NonSPDXLicensesTestSource), nameof(NonSPDXLicensesTestSource.GetCases))]
        public async Task Fast_License_Matcher_Should_Not_Pick_A_License(string licenseText)
        {
            await Assert.That(FastlicenseMatcher.Match(licenseText)).IsEmpty();
        }
    }
}
