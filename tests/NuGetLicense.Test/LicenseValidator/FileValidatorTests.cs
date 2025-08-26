// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetLicense.LicenseValidator;
using NuGetLicense.LicenseValidator.FileLicense;

namespace NuGetLicense.Test.LicenseValidator;

[TestFixture]
internal class FileValidatorTests
{
    private static readonly string[] s_licenseKeys = FileLicenseMap.Map.Keys.ToArray();

    [Test]
    public void ValidatingEmptyContent_Should_ReturnNull()
    {
        // Arrange
        string content = string.Empty;

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(content);

        // Assert
        Assert.That(result, Is.Null, "Expected null result for empty content");
    }

    [Test]
    [TestCaseSource(nameof(s_licenseKeys))]
    public void ValidatingContentWithLicense_Should_ReturnLicense(string expected)
    {
        // Arrange
        string content = FileLicenseMap.Map[expected];

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(content);

        // Assert
        Assert.That(result, Is.Not.Null, "Expected non-null result for content with license");
        Assert.That(result, Is.EqualTo(expected),
            $"Expected result to match the license key for content: {content}");
    }

    [Test]
    [TestCase("Apache-2.0", 90, ExpectedResult = License.Apache2)]
    [TestCase("MIT", 90, ExpectedResult = License.Mit)]
    [TestCase("BSD-2-Clause", 90, ExpectedResult = License.Bsd2)]
    public string? ValidatingModifiedLicense_WithMinorChanges_Should_Match(string licenseKey, int threshold)
    {
        // Arrange - Get original license and make minor modifications
        string originalLicense = FileLicenseMap.Map[licenseKey];
        string modifiedLicense = originalLicense
                .Replace("[yyyy]", "2025") // Apache changes
                .Replace("[name of copyright owner]", "My Company") // Apache changes
                .Replace("<YEAR>", "2025") // BSD-2 changes
                .Replace("<COPYRIGHT HOLDER>", "My Company") // BSD-2 changes
                .Replace("Copyright (c) 2018", "Copyright (c) 2025") // MIT changes
                .Replace("<copyright holders>", "My Company") // MIT changes
            ;

        // Act
        Assert.That(modifiedLicense, Is.Not.EqualTo(originalLicense), "Modified license should differ from original");
        return FileLicenseMatcher.FindBestMatch(modifiedLicense, threshold);
    }

    [Test]
    [TestCase("Apache-2.0")]
    [TestCase("MIT")]
    [TestCase("BSD-3-Clause")]
    public void ValidatingLicense_WithSubstantialChanges_Should_NotMatch(string licenseKey)
    {
        // Arrange - Get original license and make substantial modifications
        string originalLicense = FileLicenseMap.Map[licenseKey];
        string heavilyModifiedLicense = originalLicense
            .Replace("permission", "restriction")
            .Replace("granted", "denied")
            .Replace("free", "paid")
            .Substring(0, originalLicense.Length / 3); // Keep only first third

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(heavilyModifiedLicense, 90);

        // Assert
        Assert.That(heavilyModifiedLicense, Is.Not.EqualTo(originalLicense), "Heavily modified license should differ from original");
        Assert.That(result, Is.Null, $"Expected no match for heavily modified {licenseKey} license");
    }

    [Test]
    public void ValidatingApacheLicense_WithTypicalModifications_Should_Match()
    {
        // Arrange - Apache license with common real-world modifications
        string modifiedApache = FileLicenseMap.Map[License.Apache2]
            .Replace("Copyright [yyyy] [name of copyright owner]", "Copyright 2024 Acme Corporation")
            .Replace("APPENDIX", ""); // Remove appendix section

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(modifiedApache, 85);

        // Assert
        Assert.That(result, Is.EqualTo(License.Apache2));
    }

    [Test]
    public void ValidatingMitLicense_WithCustomization_Should_Match()
    {
        // Arrange - MIT license with typical customizations
        string modifiedMit = FileLicenseMap.Map[License.Mit]
            .Replace("<year>", "2024")
            .Replace("<copyright holders>", "Jane Smith and Contributors")
            .Replace("THE SOFTWARE", "THIS SOFTWARE PACKAGE");

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(modifiedMit, 85);

        // Assert
        Assert.That(result, Is.EqualTo(License.Mit));
    }

    [Test]
    public void ValidatingRandomText_Should_NotMatch()
    {
        // Arrange
        string randomText = "This is just some random text that has nothing to do with licenses. " +
                           "It talks about cats, dogs, and programming in general.";

        // Act
        string? result = FileLicenseMatcher.FindBestMatch(randomText, 90);

        // Assert
        Assert.That(result, Is.Null, "Expected no match for random text");
    }
}
