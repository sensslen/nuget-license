// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Concurrent;
using System.Collections.Immutable;
using FileLicenseMatcher;
using NuGetUtility.Extensions;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.HttpClientWrapper;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging;
using NuGetUtility.Wrapper.NuGetWrapper.Packaging.Core;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;
using Tethys.SPDX.ExpressionParser;

namespace NuGetLicense.LicenseValidator
{
    public class LicenseValidator(IImmutableDictionary<Uri, string> licenseMapping,
                                  IEnumerable<string> allowedLicenses,
                                  IFileDownloader fileDownloader,
                                  IFileLicenseMatcher fileLicenseMatcher,
                                  string[] ignoredPackages)
    {
        public async Task<IEnumerable<LicenseValidationResult>> Validate(IAsyncEnumerable<ReferencedPackageWithContext> packages,
                                                                         CancellationToken token)
        {
            var result = new ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult>();
            await foreach (ReferencedPackageWithContext info in packages.WithCancellation(token))
            {
                if (IsIgnoredPackage(info.PackageInfo.Identity))
                {
                    AddOrUpdateLicense(result,
                        info.PackageInfo,
                        LicenseInformationOrigin.Ignored);
                }
                else if (info.PackageInfo.LicenseMetadata is not null)
                {
                    await ValidateLicenseByMetadataAsync(info.PackageInfo, info.Context, result, token);
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
                        new ValidationError("No license information found", info.Context));
                }
            }
            return result.Values;
        }

        private bool IsIgnoredPackage(PackageIdentity identity)
        {
            return Array.Exists(ignoredPackages, ignored => identity.Id.Like(ignored));
        }

        private static void AddOrUpdateLicense(ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
                                               IPackageMetadata info,
                                               LicenseInformationOrigin origin,
                                               ValidationError error,
                                               string? license = null)
        {
            var newValue = new LicenseValidationResult(info.Identity.Id,
                                                       info.Identity.Version,
                                                       info.ProjectUrl,
                                                       license,
                                                       info.LicenseUrl?.AbsoluteUri,
                                                       info.Copyright,
                                                       info.Authors,
                                                       info.Description,
                                                       info.Summary,
                                                       origin,
                                                       [error]);
            result.AddOrUpdate(new LicenseNameAndVersion(info.Identity.Id, info.Identity.Version),
                               newValue,
                               (key, oldValue) => UpdateResult(oldValue, newValue));
        }

        private static void AddOrUpdateLicense(ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
                                               IPackageMetadata info,
                                               LicenseInformationOrigin origin,
                                               string? license = null)
        {
            var newValue = new LicenseValidationResult(info.Identity.Id,
                                                       info.Identity.Version,
                                                       info.ProjectUrl,
                                                       license,
                                                       info.LicenseUrl?.AbsoluteUri,
                                                       info.Copyright,
                                                       info.Authors,
                                                       info.Description,
                                                       info.Summary,
                                                       origin);
            result.AddOrUpdate(new LicenseNameAndVersion(info.Identity.Id, info.Identity.Version),
                               newValue,
                               (key, oldValue) => UpdateResult(oldValue, newValue));
        }

        private static LicenseValidationResult UpdateResult(LicenseValidationResult oldValue,
                                                            LicenseValidationResult newValue)
        {
            oldValue.ValidationErrors.AddRange(newValue.ValidationErrors);
            if (oldValue.License is null && newValue.License is not null)
            {
                oldValue.License = newValue.License;
                oldValue.LicenseInformationOrigin = newValue.LicenseInformationOrigin;
            }
            return oldValue;
        }

        private async Task ValidateLicenseByMetadataAsync(IPackageMetadata info,
                                                          string context,
                                                          ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
                                                          CancellationToken token)
        {
            switch (info.LicenseMetadata!.Type)
            {
                case LicenseType.Expression:
                case LicenseType.Overwrite:
                    {
                        string licenseId = info.LicenseMetadata!.License;
                        SpdxExpression? licenseExpression = ParseSpdxExpression(licenseId);
                        if (IsValidLicenseExpression(licenseExpression))
                        {
                            await DownloadLicenseAsync(GetLicenseUrl(licenseId), info.Identity, context, token);
                            AddOrUpdateLicense(result,
                                               info,
                                               ToLicenseOrigin(info.LicenseMetadata.Type),
                                               info.LicenseMetadata.License);
                        }
                        else
                        {
                            AddOrUpdateLicense(result,
                                               info,
                                               ToLicenseOrigin(info.LicenseMetadata.Type),
                                               new ValidationError(GetLicenseNotAllowedMessage(info.LicenseMetadata.License), context),
                                               info.LicenseMetadata.License);
                        }
                        break;
                    }
                case LicenseType.File:
                    {
                        string matchedLicense = fileLicenseMatcher.Match(info.LicenseMetadata.License);

                        if (string.IsNullOrEmpty(matchedLicense))
                        {
                            AddOrUpdateLicense(result,
                                               info,
                                               LicenseInformationOrigin.File,
                                               new ValidationError("Unable to determine license from the given license file", context),
                                               info.LicenseMetadata.License);
                            break;
                        }

                        SpdxExpression? licenseExpression = ParseSpdxExpression(matchedLicense);
                        if (IsValidLicenseExpression(licenseExpression))
                        {
                            await fileDownloader.StoreFileAsync(info.LicenseMetadata.License, GetFileName(info.Identity), token);
                            AddOrUpdateLicense(result,
                                               info,
                                               LicenseInformationOrigin.File,
                                               matchedLicense);
                        }
                        else
                        {
                            AddOrUpdateLicense(result,
                                               info,
                                               LicenseInformationOrigin.File,
                                               new ValidationError(GetLicenseNotAllowedMessage(matchedLicense), context),
                                               info.LicenseMetadata.License);
                        }
                        break;
                    }
                default:
                    {
                        AddOrUpdateLicense(result,
                                           info,
                                           LicenseInformationOrigin.Unknown,
                                           new ValidationError($"Validation for licenses of type {info.LicenseMetadata!.Type} not yet supported",
                                                               context));
                        break;
                    }
            }
        }

        private bool IsValidLicenseExpression(SpdxExpression? expression) => expression switch
        {
            SpdxAndExpression and => IsValidLicenseExpression(and.Left) && IsValidLicenseExpression(and.Right),
            SpdxOrExpression or => IsValidLicenseExpression(or.Left) || IsValidLicenseExpression(or.Right),
            SpdxWithExpression or SpdxLicenseExpression or SpdxLicenseReference => IsLicenseValid(expression.ToString()),
            _ => false,
        };

        private async Task ValidateLicenseByUrl(IPackageMetadata info,
                                                string context,
                                                ConcurrentDictionary<LicenseNameAndVersion, LicenseValidationResult> result,
                                                CancellationToken token)
        {
            if (info.LicenseUrl!.IsAbsoluteUri)
            {
                await DownloadLicenseAsync(info.LicenseUrl, info.Identity, context, token);
            }

            if (licenseMapping.TryGetValue(info.LicenseUrl, out string? licenseId))
            {
                SpdxExpression? licenseExpression = ParseSpdxExpression(licenseId);

                if (IsValidLicenseExpression(licenseExpression))
                {
                    AddOrUpdateLicense(result,
                                       info,
                                       LicenseInformationOrigin.Url,
                                       licenseId);
                }
                else
                {
                    AddOrUpdateLicense(result,
                                       info,
                                       LicenseInformationOrigin.Url,
                                       new ValidationError(GetLicenseNotAllowedMessage(licenseId), context),
                                       licenseId);
                }
            }
            else if (!allowedLicenses.Any())
            {
                AddOrUpdateLicense(result,
                                   info,
                                   LicenseInformationOrigin.Url,
                                   info.LicenseUrl.ToString());
            }
            else
            {
                AddOrUpdateLicense(result,
                                   info,
                                   LicenseInformationOrigin.Url,
                                   new ValidationError($"Cannot determine License type for url {info.LicenseUrl}", context),
                                   info.LicenseUrl.ToString());
            }
        }

        private static Uri FixupLicenseUrl(Uri licenseUrl)
        {
            if ((licenseUrl.Host == "github.com" || licenseUrl.Host == "www.github.com")
                && licenseUrl is { Query: "", Segments: [_, _, _, "blob/", _, ..] })
            {
                return new Uri($"{licenseUrl}?raw=true");
            }
            return licenseUrl;
        }

        private async Task DownloadLicenseAsync(Uri licenseUrl, PackageIdentity identity, string context, CancellationToken token)
        {
            licenseUrl = FixupLicenseUrl(licenseUrl);
            try
            {
                await fileDownloader.DownloadFile(licenseUrl, GetFileName(identity), token);
            }
            catch (OperationCanceledException)
            {
                // swallow cancellation
            }
            catch (Exception e)
            {
                throw new LicenseDownloadException(e, context, licenseUrl, identity);
            }
        }

        private bool IsLicenseValid(string licenseId)
        {
            if (!allowedLicenses.Any())
            {
                return true;
            }

            return allowedLicenses.Any(allowedLicense => allowedLicense.Equals(licenseId));
        }

        private static string GetLicenseNotAllowedMessage(string license) => $"License \"{license}\" not found in list of supported licenses";

        private static Uri GetLicenseUrl(string spdxIdentifier) => new($"https://licenses.nuget.org/({spdxIdentifier})");

        private static LicenseInformationOrigin ToLicenseOrigin(LicenseType type) => type switch
        {
            LicenseType.Overwrite => LicenseInformationOrigin.Overwrite,
            LicenseType.Expression => LicenseInformationOrigin.Expression,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"This conversion method only supports the {nameof(LicenseType.Overwrite)} and {nameof(LicenseType.Expression)} types for conversion")
        };

        private static string GetFileName(PackageIdentity identity) => $"{identity.Id}__{identity.Version}";

        private static SpdxExpression? ParseSpdxExpression(string expression) => SpdxExpressionParser.Parse(expression, _ => true, _ => true);

        private sealed record LicenseNameAndVersion(string LicenseName, INuGetVersion Version);
    }
}
