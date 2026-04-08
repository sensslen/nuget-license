// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Immutable;
using AutoFixture;
using FileLicenseMatcher;
using NSubstitute;
using NuGetLicense.LicenseValidator;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Test.Extensions.Helper.AsyncEnumerableExtension;
using NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetLicense.Test.LicenseValidator
{
    public class LicenseValidatorTest
    {
        public LicenseValidatorTest()
        {
            _fixture = new Fixture();
            _fixture.Customizations.Add(new NuGetVersionBuilder());
            _fileDownloader = Substitute.For<IFileDownloader>();
            _licenseMatcher = Substitute.For<IFileLicenseMatcher>();
            _licenseMapping = ImmutableDictionary.CreateRange(_fixture.Create<Dictionary<Uri, string>>());
            _allowedLicenses = _fixture.CreateMany<string>();
            _context = _fixture.Create<string>();
            _projectUrl = _fixture.Create<Uri>();
            _ignoredLicenses = _fixture.Create<string[]>();
            _token = new CancellationTokenSource();

            _uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                _allowedLicenses,
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
        }

        [After(HookType.Test)]
        public void TearDown()
        {
            _token.Dispose();
        }

        private readonly NuGetLicense.LicenseValidator.LicenseValidator _uut;
        private readonly IImmutableDictionary<Uri, string> _licenseMapping;
        private readonly IEnumerable<string> _allowedLicenses;
        private readonly string _context;
        private readonly IFileDownloader _fileDownloader;
        private readonly Uri _projectUrl;
        private readonly string[] _ignoredLicenses;
        private readonly CancellationTokenSource _token;
        private readonly IFileLicenseMatcher _licenseMatcher;
        private readonly IFixture _fixture;

        [Test]
        public async Task ValidatingEmptyList_Should_ReturnEmptyValidatedLicenses()
        {
            IAsyncEnumerable<ReferencedPackageWithContext> emptyListToValidate = Enumerable.Empty<ReferencedPackageWithContext>().AsAsyncEnumerable();
            IEnumerable<LicenseValidationResult> results = await _uut.Validate(emptyListToValidate, _token.Token);
            await Assert.That(results).IsEmpty();
        }

        private IPackageMetadata SetupPackage(string packageId, INuGetVersion packageVersion)
        {
            IPackageMetadata packageInfo = Substitute.For<IPackageMetadata>();
            packageInfo.Identity.Returns(new PackageIdentity(packageId, packageVersion));
            packageInfo.ProjectUrl.Returns(_projectUrl.ToString());
            return packageInfo;
        }

        private IPackageMetadata SetupPackageWithLicenseInformationOfType(string packageId,
            INuGetVersion packageVersion,
            string license,
            LicenseType type)
        {
            IPackageMetadata packageInfo = SetupPackage(packageId, packageVersion);
            packageInfo.LicenseMetadata.Returns(new LicenseMetadata(type, license));
            return packageInfo;
        }
        private IPackageMetadata SetupPackageWithCopyright(string packageId,
            INuGetVersion packageVersion,
            string copyrigth)
        {
            IPackageMetadata packageInfo = SetupPackage(packageId, packageVersion);
            packageInfo.Copyright.Returns(copyrigth);
            return packageInfo;
        }
        private IPackageMetadata SetupPackageWithAuthors(string packageId,
            INuGetVersion packageVersion,
            string authors)
        {
            IPackageMetadata packageInfo = SetupPackage(packageId, packageVersion);
            packageInfo.Authors.Returns(authors);
            return packageInfo;
        }

        private IPackageMetadata SetupPackageWithExpressionLicenseInformation(string packageId,
            INuGetVersion packageVersion,
            string license)
        {
            return SetupPackageWithLicenseInformationOfType(packageId, packageVersion, license, LicenseType.Expression);
        }

        private IPackageMetadata SetupPackageWithOverwriteLicenseInformation(string packageId,
            INuGetVersion packageVersion,
            string license)
        {
            return SetupPackageWithLicenseInformationOfType(packageId, packageVersion, license, LicenseType.Overwrite);
        }

        private static IAsyncEnumerable<ReferencedPackageWithContext> CreateInput(IPackageMetadata metadata,
            string context)
        {
            return new[] { new ReferencedPackageWithContext(context, metadata) }.AsAsyncEnumerable();
        }

        [Test]
        public async Task ValidatingLicenses_Should_IgnorePackage_If_PackageNameMatchesExactly()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append(packageId).ToArray());

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicenses_Should_NotIgnorePackage_If_PackageNameDoesNotMatchExactly()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append(packageId.Substring(1)).ToArray());

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, license);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        [Arguments(1)]
        [Arguments(5)]
        [Arguments(int.MaxValue)]
        public async Task ValidatingLicenses_Should_IgnorePackage_If_IgnoreWildcardMatches_If_WildcardMatchesStart(
            int matchedCharacters)
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append($"*{packageId.Substring(Math.Min(matchedCharacters, packageId.Length))}").ToArray());

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        [Arguments(0)]
        [Arguments(1)]
        [Arguments(5)]
        public async Task ValidatingLicenses_Should_IgnorePackage_If_IgnoreWildcardMatches_If_WildcardMatchesEnd(
            int remainingCharacters)
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append($"{packageId.Substring(0, remainingCharacters)}*").ToArray());

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        [Arguments(1, 2)]
        [Arguments(1, 5)]
        [Arguments(5, 10)]
        public async Task ValidatingLicenses_Should_IgnorePackage_If_IgnoreWildcardMatches_If_WildcardMatchesMiddle(
            int wildcardMatchStartIndex,
            int wildcardMatchEndIndex)
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append($"{packageId.Substring(0, wildcardMatchStartIndex)}*{packageId.Substring(wildcardMatchEndIndex)}").ToArray());

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicenses_Should_IgnorePackage_If_IgnoreWildcardMatches_If_MultipleWildcards()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append($"*{packageId.Substring(2, 5)}*{packageId.Substring(10, 2)}*").ToArray());

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectValidatedLicenseList()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, license);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectValidatedLicenseList_When_Or_Expression()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license1 = _fixture.Create<string>();
            string license2 = _fixture.Create<string>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            string expression = $"{license1} OR {license2}";

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectValidatedLicenseList_When_And_Expression()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license1 = _fixture.Create<string>();
            string license2 = _fixture.Create<string>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            string expression = $"{license1} AND {license2}";

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpression_Should_StartDownloadingSaidLicense()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license1 = _fixture.Create<string>();
            string license2 = _fixture.Create<string>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            string expression = $"{license1} AND {license2}";

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            _ = await uut.Validate(CreateInput(package, _context), _token.Token);

            await _fileDownloader.Received(1).DownloadFile(new Uri($"https://licenses.nuget.org/({expression})"),
                    $"{package.Identity.Id}__{package.Identity.Version}",
                    _token.Token);
        }

        [Test]
        public async Task ValidatingLicensesWithOverwriteLicenseInformation_Should_GiveCorrectValidatedLicenseList()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            IPackageMetadata package = SetupPackageWithOverwriteLicenseInformation(packageId, packageVersion, license);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Overwrite)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        private IPackageMetadata SetupPackageWithLicenseUrl(string packageId,
            INuGetVersion packageVersion,
            Uri url)
        {
            IPackageMetadata packageInfo = SetupPackage(packageId, packageVersion);
            packageInfo.LicenseUrl.Returns(url);
            return packageInfo;
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingLicenseUrl_Should_GiveCorrectValidatedLicenseList_WhenMatchingId()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            KeyValuePair<Uri, string> mappingLicense = _licenseMapping.Shuffle(34561).First();
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, mappingLicense.Key);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            mappingLicense.Value,
                            mappingLicense.Key.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingLicenseUrl_Should_GiveCorrectValidatedLicenseList_When_MatchingUrl()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri licenseUrl = _fixture.Create<Uri>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, licenseUrl);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseUrl.ToString(),
                            licenseUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithFileLicenseMetadata_Should_GiveCorrectResult_When_Matched()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            string licenseId = _fixture.Create<string>();
            IPackageMetadata package = SetupPackageWithLicenseInformationOfType(packageId, packageVersion, license, LicenseType.File);
            _licenseMatcher.Match(license).Returns(licenseId);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseId,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.File,
                            [])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithFileLicenseMetadata_Should_GiveCorrectResult_When_Not_Matched()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();

            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            IPackageMetadata package = SetupPackageWithLicenseInformationOfType(packageId, packageVersion, license, LicenseType.File);
            _licenseMatcher.Match(license).Returns(string.Empty);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.File,
                            [
                                new ValidationError("Unable to determine license from the given license file", _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithoutLicenseInformation_Should_GiveCorrectResult()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);

            IPackageMetadata package = SetupPackage(packageId, packageVersion);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Unknown,
                            [
                                new ValidationError("No license information found",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_If_NotAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();
            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, license);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression,
                            [
                                new ValidationError($"License \"{license}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_WithOrExpression_If_NoneAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string[] licenses = _fixture.Create<string[]>();
            string expression = licenses.Length switch
            {
                0 => string.Empty,
                1 => licenses[0],
                2 => $"{licenses[0]} OR {licenses[1]}",
                _ => licenses.Skip(2).Aggregate($"{licenses[0]} OR {licenses[1]}", (expression, newLicense) => $"{newLicense} OR ({expression})")
            };

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression,
                            [
                                new ValidationError($"License \"{expression}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_WithAndExpression_If_OneNotAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string unallowedLicense = _fixture.Create<string>();
            string[] licenses = _allowedLicenses.Shuffle(135643).Append(unallowedLicense).ToArray();

            string expression = licenses.Length switch
            {
                0 => string.Empty,
                1 => licenses[0],
                2 => $"{licenses[0]} AND {licenses[1]}",
                _ => licenses.Skip(2).Aggregate($"{licenses[0]} AND {licenses[1]}", (expression, newLicense) => $"{newLicense} AND ({expression})")
            };

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression,
                            [
                                new ValidationError($"License \"{expression}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithOverwriteLicenseInformation_Should_GiveCorrectResult_If_NotAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string license = _fixture.Create<string>();
            IPackageMetadata package = SetupPackageWithOverwriteLicenseInformation(packageId, packageVersion, license);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            license,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Overwrite,
                            [
                                new ValidationError($"License \"{license}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_If_Allowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string validLicense = _allowedLicenses.Shuffle(135643).First();
            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, validLicense);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            validLicense,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_WithOrExpression_If_OneAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string unallowedLicense = _fixture.Create<string>();
            string expression = $"{_allowedLicenses.Shuffle(13563).First()} OR {unallowedLicense}";

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithExpressionLicenseInformation_Should_GiveCorrectResult_WithAndExpression_If_AllAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string[] licenses = _allowedLicenses.Shuffle(135643).Take(2).ToArray();

            string expression = $"{licenses[0]} AND {licenses[1]}";

            IPackageMetadata package = SetupPackageWithExpressionLicenseInformation(packageId, packageVersion, expression);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);
            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            expression,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Expression)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithOverwriteLicenseInformation_Should_GiveCorrectResult_If_Allowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string validLicense = _allowedLicenses.Shuffle(135643).First();
            IPackageMetadata package = SetupPackageWithOverwriteLicenseInformation(packageId, packageVersion, validLicense);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            validLicense,
                            null,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Overwrite)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_GiveCorrectResult_If_NotAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            KeyValuePair<Uri, string> urlMatch = _licenseMapping.Shuffle(765).First();
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, urlMatch.Key);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            urlMatch.Value,
                            urlMatch.Key.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url,
                            [
                                new ValidationError(
                                    $"License \"{urlMatch.Value}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithUrlInformation_Should_StartDownloadingSaidLicense()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            KeyValuePair<Uri, string> urlMatch = _licenseMapping.Shuffle(4567).First();
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, urlMatch.Key);

            _ = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await _fileDownloader.Received(1).DownloadFile(package.LicenseUrl!,
                    $"{package.Identity.Id}__{package.Identity.Version}",
                    _token.Token);
        }

        [Test]
        public async Task ValidatingLicensesWithGithubWebUiUrl_Should_StartDownloadingLicenseAsRaw()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string ghUrl = "https://github.com/dotnet/corefx/blob/master/LICENSE.TXT";
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, new Uri(ghUrl));

            _ = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await _fileDownloader.Received(1).DownloadFile(new Uri(ghUrl + "?raw=true"),
                    $"{package.Identity.Id}__{package.Identity.Version}",
                    _token.Token);
        }

        [Test]
        public async Task ValidatingLicensesWithUrlInformation_Should_ThrowLicenseDownloadInformation_If_DownloadThrows()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            KeyValuePair<Uri, string> urlMatch = _licenseMapping.Shuffle(12345).First();
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, urlMatch.Key);
            _fileDownloader.When(m => m.DownloadFile(package.LicenseUrl!, Arg.Any<string>(), Arg.Any<CancellationToken>()))
                .Do(_ => throw new Exception());

            LicenseDownloadException? exception = await Assert.That(async () => await _uut.Validate(CreateInput(package, _context), _token.Token))
                .ThrowsExactly<LicenseDownloadException>();
            await Assert.That(exception!.InnerException).IsTypeOf<Exception>();
            await Assert.That(exception.Message).IsEqualTo($"Failed to download license for package {packageId} ({packageVersion}) from url: {urlMatch.Key}.\nContext: {_context}");
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_GiveCorrectResult_If_Allowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            KeyValuePair<Uri, string> urlMatch = _licenseMapping.Shuffle(43562).First();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                _allowedLicenses.Append(urlMatch.Value),
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, urlMatch.Key);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            urlMatch.Value,
                            urlMatch.Key.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithNotMatchingUrlInformation_Should_GiveCorrectResult_If_NotAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri licenseUrl = _fixture.Create<Uri>();
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, licenseUrl);

            IEnumerable<LicenseValidationResult> result = await _uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseUrl.ToString(),
                            licenseUrl.ToString(),
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url,
                            [
                                new ValidationError($"Cannot determine License type for url {licenseUrl}",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_GiveCorrectResult_WithOrExpression_If_OneAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri orLicenseUri = _fixture.Create<Uri>();
            string firstLicense = _fixture.Create<string>();
            string secondLicense = _fixture.Create<string>();
            string licenseExpression = $"{firstLicense} OR {secondLicense}";
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping.Add(orLicenseUri, licenseExpression),
                _allowedLicenses.Append(firstLicense),
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, orLicenseUri);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseExpression,
                            orLicenseUri.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_GiveCorrectResult_WithAndExpression_If_AllAllowed()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri orLicenseUri = _fixture.Create<Uri>();
            string firstLicense = _fixture.Create<string>();
            string secondLicense = _fixture.Create<string>();
            string licenseExpression = $"{firstLicense} AND {secondLicense}";
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping.Add(orLicenseUri, licenseExpression),
                _allowedLicenses.Append(firstLicense).Append(secondLicense),
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, orLicenseUri);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseExpression,
                            orLicenseUri.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }
        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_Create_ValidationError_If_None_Is_Not_Supported()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri orLicenseUri = _fixture.Create<Uri>();
            string firstLicense = _fixture.Create<string>();
            string secondLicense = _fixture.Create<string>();
            string licenseExpression = $"{firstLicense} OR {secondLicense}";
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping.Add(orLicenseUri, licenseExpression),
                _allowedLicenses,
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, orLicenseUri);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseExpression,
                            orLicenseUri.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url,
                            [
                                new ValidationError(
                                    $"License \"{licenseExpression}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_Create_ValidationError_If_Second_Is_Not_Supported()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri orLicenseUri = _fixture.Create<Uri>();
            string firstLicense = _fixture.Create<string>();
            string secondLicense = _fixture.Create<string>();
            string licenseExpression = $"{firstLicense} AND {secondLicense}";
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping.Add(orLicenseUri, licenseExpression),
                _allowedLicenses.Append(firstLicense),
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, orLicenseUri);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseExpression,
                            orLicenseUri.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url,
                            [
                                new ValidationError(
                                    $"License \"{licenseExpression}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicensesWithMatchingUrlInformation_Should_Create_ValidationError_If_First_Is_Not_Supported()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            Uri orLicenseUri = _fixture.Create<Uri>();
            string firstLicense = _fixture.Create<string>();
            string secondLicense = _fixture.Create<string>();
            string licenseExpression = $"{firstLicense} AND {secondLicense}";
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping.Add(orLicenseUri, licenseExpression),
                _allowedLicenses.Append(secondLicense),
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses);
            IPackageMetadata package = SetupPackageWithLicenseUrl(packageId, packageVersion, orLicenseUri);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            licenseExpression,
                            orLicenseUri.AbsoluteUri,
                            null,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Url,
                            [
                                new ValidationError(
                                    $"License \"{licenseExpression}\" not found in list of supported licenses",
                                    _context)
                            ])
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicenses_ShouldContainCopyright()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string copyright = _fixture.Create<string>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append(packageId).ToArray());

            IPackageMetadata package = SetupPackageWithCopyright(packageId, packageVersion, copyright);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            copyright,
                            null,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }

        [Test]
        public async Task ValidatingLicenses_ShouldContainAuthors()
        {
            string packageId = _fixture.Create<string>();
            INuGetVersion packageVersion = _fixture.Create<INuGetVersion>();
            string authors = _fixture.Create<string>();
            var uut = new NuGetLicense.LicenseValidator.LicenseValidator(_licenseMapping,
                [],
                _fileDownloader,
                _licenseMatcher,
                _ignoredLicenses.Append(packageId).ToArray());

            IPackageMetadata package = SetupPackageWithAuthors(packageId, packageVersion, authors);

            IEnumerable<LicenseValidationResult> result = await uut.Validate(CreateInput(package, _context), _token.Token);

            await Assert.That(result).IsEquivalentTo(
                    [
                        new LicenseValidationResult(packageId,
                            packageVersion,
                            _projectUrl.ToString(),
                            null,
                            null,
                            null,
                            authors,
                            null,
                            null,
                            LicenseInformationOrigin.Ignored)
                    ]).Using(new LicenseValidationResultValueEqualityComparer());
        }
    }

}
