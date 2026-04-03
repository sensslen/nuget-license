// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using Bogus;
using NuGetLicense.LicenseValidator;
using NuGetLicense.Output;
using NuGetUtility.Test.Extensions;
using NuGetUtility.Test.Extensions.Helper.ShuffelledEnumerable;
using HelperNuGetVersion = NuGetLicense.Test.Output.Helper.NuGetVersion;

namespace NuGetLicense.Test.Output
{
    public abstract class TestBase
    {
        protected TestBase(bool includeCopyright, bool includeAuthors, bool includeLicenseUrl)
        {
            _validatedLicenseFaker = new Faker<LicenseValidationResult>().CustomInstantiator(f =>
                    new LicenseValidationResult(f.Name.JobTitle(),
                        new HelperNuGetVersion(f.System.Semver()),
                        GetNullable(f, f.Internet.Url),
                        GetNullable(f, f.Hacker.Phrase),
                        includeLicenseUrl ? GetNullable(f, f.Hacker.Phrase) : null,
                        includeCopyright ? GetNullable(f, f.Hacker.Phrase) : null,
                        includeAuthors ? GetNullable(f, () => string.Join(",", Enumerable.Repeat(true, f.Random.Int(0, 10)).Select(_ => f.Person.FullName))) : null,
                        GetNullable(f, () => f.Lorem.Sentence()),
                        GetNullable(f, () => f.Lorem.Sentence()),
                        f.Random.Enum<LicenseInformationOrigin>()))
                .UseSeed(8675309);
            _licenseValidationErrorFaker = new Faker<LicenseValidationResult>().CustomInstantiator(f =>
                    new LicenseValidationResult(f.Name.JobTitle(),
                        new HelperNuGetVersion(f.System.Semver()),
                        GetNullable(f, f.Internet.Url),
                        GetNullable(f, f.Hacker.Phrase),
                        includeLicenseUrl ? GetNullable(f, f.Hacker.Phrase) : null,
                        includeCopyright ? GetNullable(f, f.Hacker.Phrase) : null,
                        includeAuthors ? GetNullable(f, () => string.Join(",", Enumerable.Repeat(true, f.Random.Int(0, 10)).Select(_ => f.Person.FullName))) : null,
                        GetNullable(f, () => f.Lorem.Sentence()),
                        GetNullable(f, () => f.Lorem.Sentence()),
                        f.Random.Enum<LicenseInformationOrigin>(),
                        GetErrorList(f).ToList()))
                .UseSeed(9078345);
        }

        private IOutputFormatter _uut = null!;
        private readonly Faker<LicenseValidationResult> _licenseValidationErrorFaker;
        private readonly Faker<LicenseValidationResult> _validatedLicenseFaker;

        [Before(HookType.Test)]
        public void SetUp()
        {
            _uut = CreateUut();
        }
        protected abstract IOutputFormatter CreateUut();

        private static T? GetNullable<T>(Faker faker, Func<T> getter) where T : class
        {
            if (faker.Random.Bool())
            {
                return null;
            }
            return getter();
        }

        private static IEnumerable<ValidationError> GetErrorList(Faker faker)
        {
            int itemCount = faker.Random.Int(3, 10);
            for (int i = 0; i < itemCount; i++)
            {
                yield return new ValidationError(faker.Name.FirstName(), faker.Internet.Url());
            }
        }

        [Test, MatrixDataSource]
        public async Task ValidatedLicensesWithErrors_Should_PrintCorrectTable(
            [Matrix(0, 1, 5, 20, 100)] int validCount,
            [Matrix(1, 3, 5, 20)] int errorCount)
        {
            using var stream = new MemoryStream();
            var result = _licenseValidationErrorFaker.GenerateForever()
                .Take(errorCount)
                .Concat(_validatedLicenseFaker.GenerateForever().Take(validCount))
                .Shuffle(971234)
                .ToList();
            await _uut.Write(stream, result);

            await Verify(stream.AsString()).HashParameters();
        }

        [Test, MatrixDataSource]
        public async Task ValidatedLicenses_Should_PrintCorrectTable(
            [Matrix(0, 1, 5, 20, 100)] int validatedLicenseCount)
        {
            using var stream = new MemoryStream();
            var validated = _validatedLicenseFaker.GenerateForever().Take(validatedLicenseCount).ToList();
            await _uut.Write(stream, validated);

            await Verify(stream.AsString()).HashParameters();
        }
    }
}
