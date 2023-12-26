using System.Collections.Concurrent;
using System.Collections.Immutable;
using NuGetUtility.Extensions;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.LicenseValidator
{
    public class LicenseValidator
    {
        private readonly IEnumerable<string> _allowedLicenses;
        private readonly IFileDownloader _fileDownloader;
        private readonly string[] _ignoredPackages;
        private readonly IImmutableDictionary<Uri, string> _licenseMapping;

        public LicenseValidator(IImmutableDictionary<Uri, string> licenseMapping,
            IEnumerable<string> allowedLicenses,
            IFileDownloader fileDownloader,
            string[] ignoredPackages)
        {
            _licenseMapping = licenseMapping;
            _allowedLicenses = allowedLicenses;
            _fileDownloader = fileDownloader;
            _ignoredPackages = ignoredPackages;
        }

        public async Task<IEnumerable<LicenseValidationResult>> Validate(
            IAsyncEnumerable<ReferencedPackageWithContext> packages,
            CancellationToken token)
        {
            var result = new ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult>();
            await foreach (ReferencedPackageWithContext info in packages)
            {
                if (IsIgnoredPackage(info.PackageInfo.Identity))
                {
                    AddOrUpdateLicense(result,
                        info.PackageInfo,
                        LicenseInformationOrigin.Ignored,
                        new ValidationCheck(info.Context));
                }
                else if (info.PackageInfo.LicenseMetadata is not null)
                {
                    ValidateLicenseByMetadata(info.PackageInfo, info.Context, result);
                }
                else if (info.PackageInfo.LicenseUrl is not null)
                {
                    await ValidateLicenseByUrl(info.PackageInfo, info.Context, result, token);
                }
                else
                {
                    AddOrUpdateLicense(result,
                        info.PackageInfo,
                        LicenseInformationOrigin.Unknown,
                        new ValidationCheck(info.Context, "No license information found"));
                }
            }
            return result.Values;
        }

        private bool IsIgnoredPackage(PackageIdentity identity)
        {
            return Array.Exists(_ignoredPackages, ignored => identity.Id.Like(ignored));
        }

        private void AddOrUpdateLicense(
            ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
            IPackageMetadata info,
            LicenseInformationOrigin origin,
            ValidationCheck check,
            string? license = null)
        {
            var newValue = new LicenseValidationResult(
                info.Identity.Id,
                info.Identity.Version,
                info.ProjectUrl?.ToString(),
                license,
                origin,
                new List<ValidationCheck> { check });
            result.AddOrUpdate(new LicenseNameAndVersion(info.Identity.Id, info.Identity.Version),
                newValue,
                (key, oldValue) => UpdateResult(oldValue, newValue));
        }

        private LicenseValidationResult UpdateResult(LicenseValidationResult oldValue,
            LicenseValidationResult newValue)
        {
            oldValue.ValidationChecks.AddRange(newValue.ValidationChecks);
            if (oldValue.License is null && newValue.License is not null)
            {
                oldValue.License = newValue.License;
                oldValue.LicenseInformationOrigin = newValue.LicenseInformationOrigin;
            }
            return oldValue;
        }

        private void ValidateLicenseByMetadata(IPackageMetadata info,
            string context,
            ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result)
        {
            switch (info.LicenseMetadata!.Type)
            {
                case LicenseType.Expression:
                case LicenseType.Overwrite:
                    string licenseId = info.LicenseMetadata!.License;
                    if (IsLicenseValid(licenseId))
                    {
                        AddOrUpdateLicense(result,
                            info,
                            ToLicenseOrigin(info.LicenseMetadata.Type),
                            new ValidationCheck(context),
                            info.LicenseMetadata.License);
                    }
                    else
                    {
                        AddOrUpdateLicense(result,
                            info,
                            ToLicenseOrigin(info.LicenseMetadata.Type),
                            new ValidationCheck(context, GetLicenseNotAllowedMessage(info.LicenseMetadata.License)),
                            info.LicenseMetadata.License);
                    }

                    break;
                default:
                    AddOrUpdateLicense(result,
                        info,
                        LicenseInformationOrigin.Unknown,
                        new ValidationCheck(
                            context,
                            $"Validation for licenses of type {info.LicenseMetadata!.Type} not yet supported"));
                    break;
            }
        }

        private async Task ValidateLicenseByUrl(IPackageMetadata info,
            string context,
            ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
            CancellationToken token)
        {
            if (info.LicenseUrl!.IsAbsoluteUri)
            {
                try
                {
                    await _fileDownloader.DownloadFile(info.LicenseUrl,
                        $"{info.Identity.Id}__{info.Identity.Version}.html",
                        token);
                }
                catch (OperationCanceledException)
                {
                    // swallow cancellation
                }
                catch (Exception e)
                {
                    throw new LicenseDownloadException(e, context, info.Identity);
                }
            }

            if (_licenseMapping.TryGetValue(info.LicenseUrl, out string? licenseId))
            {
                if (IsLicenseValid(licenseId))
                {
                    AddOrUpdateLicense(result,
                        info,
                        LicenseInformationOrigin.Url,
                        new ValidationCheck(context),
                        licenseId);
                }
                else
                {
                    AddOrUpdateLicense(result,
                        info,
                        LicenseInformationOrigin.Url,
                        new ValidationCheck(context, GetLicenseNotAllowedMessage(licenseId)),
                        licenseId);
                }
            }
            else if (!_allowedLicenses.Any())
            {
                AddOrUpdateLicense(result,
                    info,
                    LicenseInformationOrigin.Url,
                    new ValidationCheck(context),
                    info.LicenseUrl.ToString());
            }
            else
            {
                AddOrUpdateLicense(result,
                    info,
                    LicenseInformationOrigin.Url,
                    new ValidationCheck(context, $"Cannot determine License type for url {info.LicenseUrl}"),
                    info.LicenseUrl.ToString());
            }
        }

        private bool IsLicenseValid(string licenseId)
        {
            if (!_allowedLicenses.Any())
            {
                return true;
            }

            return _allowedLicenses.Any(allowedLicense => allowedLicense.Equals(licenseId));
        }

        private string GetLicenseNotAllowedMessage(string license)
        {
            return $"License {license} not found in list of supported licenses";
        }

        private LicenseInformationOrigin ToLicenseOrigin(LicenseType type) => type switch
        {
            LicenseType.Overwrite => LicenseInformationOrigin.Overwrite,
            LicenseType.Expression => LicenseInformationOrigin.Expression,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"This conversion method only supports the {nameof(LicenseType.Overwrite)} and {nameof(LicenseType.Expression)} types for conversion")
        };

        private sealed record LicenseNameAndVersion(string LicenseName, INuGetVersion Version);
    }
}
