// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetLicense.LicenseValidator;
using NuGetLicense.LicenseValidator.FileLicense;
using NuGetUtility.LicenseValidator.FileLicense;

namespace NuGetLicense.Test.LicenseValidator;

[TestFixture]
internal class FileValidatorTests
{
    private FileLicenseMatcher _fileMatcher = null!;
    private static readonly string[] LicenseKeys = FileLicenseMap.Map.Keys.ToArray();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fileMatcher = new FileLicenseMatcher();
    }

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
    [TestCaseSource(nameof(LicenseKeys))]
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
}
