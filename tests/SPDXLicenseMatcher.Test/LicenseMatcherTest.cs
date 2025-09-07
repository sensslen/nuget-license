// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher.Test
{
    public class LicenseMatcherTest
    {
        public record Case(string Identifier, string Content);

        public static class LicenseMatcherTestSource
        {
            private static readonly int s_prefixLength = "SPDXLicenseMatcher.Test.TestLicenses.".Length;
            private static readonly int s_postfixLength = ".txt".Length;
            public static IEnumerable<Func<Case>> GetCases()
            {
                var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string name in executingAssembly.GetManifestResourceNames().Where(n => n.EndsWith("txt")))
                {
                    string expectedIdentifier = name.Substring(s_prefixLength, name.Length - s_postfixLength - s_prefixLength);
                    using var reader = new StreamReader(executingAssembly.GetManifestResourceStream(name)!);
                    yield return () => new Case(expectedIdentifier, reader.ReadToEnd());
                }
            }
        }

        [Test]
        [MethodDataSource(typeof(LicenseMatcherTestSource), nameof(LicenseMatcherTestSource.GetCases))]
        public async Task License_Matcher_Should_Pick_Correct_License(Case @case)
        {
            var matcher = new LicenseMatcher();

            await Assert.That(matcher.Match(@case.Content)).Contains(@case.Identifier);
        }
    }
}
