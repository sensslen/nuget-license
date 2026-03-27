// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Concurrent;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace NuGetUtility.Test.UrlToLicenseMapping
{
    public static class UrlToLicenseMappingTestSource
    {
        public static IEnumerable<KeyValuePair<Uri, string>> GetDefaultMappings()
        {
            return NuGetLicense.LicenseValidator.UrlToLicenseMapping.Default;
        }
    }

    public class UrlToLicenseMappingTest
    {
        private const int RETRY_COUNT = 3;
        private const int MAX_CONCURRENT_DRIVERS = 5;

        private static readonly ConcurrentQueue<DisposableWebDriver> s_driverPool = new();
        private static readonly SemaphoreSlim s_driverSlots = new(MAX_CONCURRENT_DRIVERS, MAX_CONCURRENT_DRIVERS);

        [After(Class)]
        public static void TearDown()
        {
            while (s_driverPool.TryDequeue(out DisposableWebDriver? driver))
            {
                driver?.Dispose();
            }
        }

        [Test]
        [MethodDataSource(typeof(UrlToLicenseMappingTestSource), nameof(UrlToLicenseMappingTestSource.GetDefaultMappings))]
        public async Task License_Should_Be_Available_And_Match_Expected_License(KeyValuePair<Uri, string> mappedValue)
        {
            int retryCount = 0;
            int baseDelayMs = 2000;
            bool runSucceeded = false;

            using var slot = new DriverSlot(s_driverSlots);
            await slot.WaitAsync();

            // Grab an existing driver from the pool, or create a new one if the pool is empty
            if (!s_driverPool.TryDequeue(out DisposableWebDriver? driver))
            {
                driver = new DisposableWebDriver();
            }

            try
            {
                while (true)
                {
                    Result<string> licenseResult = await GetLicenseValue(mappedValue.Key, driver);

                    if (licenseResult.IsSuccess)
                    {
                        // Verify the license content matches the expected license type from the mapping
                        await Verify(licenseResult.Value).HashParameters().UseStringComparer(
                            (received, verified, context) => CompareLicense(received, verified, context, mappedValue.Value));
                        runSucceeded = true;
                        return;
                    }

                    if (retryCount >= RETRY_COUNT)
                    {
                        Assert.Fail(licenseResult.Error);
                    }

                    int retryTimeout = (int)(baseDelayMs * Math.Pow(10, retryCount)) + Random.Shared.Next(1000, 3000);
                    retryCount++;

                    Console.WriteLine($"Failed to check license. Retry count: {retryCount}\n\n");
                    Console.WriteLine($"Error:");
                    Console.WriteLine(licenseResult.Error);
                    Console.WriteLine($"\n\nRetrying after {retryTimeout}ms\n\n");

                    await Task.Delay(retryTimeout);
                }
            }
            finally
            {
                // Return the driver back to the pool so the next test case can reuse it
                if (runSucceeded)
                {
                    s_driverPool.Enqueue(driver);
                }
                else
                {
                    driver.Dispose();
                }
            }
        }

        private async Task<Result<string>> GetLicenseValue(Uri licenseUrl, DisposableWebDriver driver)
        {
            string bodyText;
            try
            {
                await driver.Navigate().GoToUrlAsync(licenseUrl.ToString());
                bodyText = driver.FindElement(By.TagName("body")).Text;
            }
            catch (WebDriverException e)
            {
                return new() { Error = $"Failed to navigate to {licenseUrl}.\n{e}" };
            }

            if (bodyText.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                return new() { Error = $"Rate limit exceeded:\n{bodyText}" };
            }

            return new() { Value = bodyText };
        }

        private static Task<CompareResult> CompareLicense(string received, string verified, IReadOnlyDictionary<string, object> context, string expectedLicenseType)
        {
            string trimmedReceived = string.Join(' ', received.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));
            string trimmedVerified = string.Join(' ', verified.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));
            
            // First check if the received content contains the verified content
            bool contentMatches = !string.IsNullOrWhiteSpace(trimmedVerified) && trimmedReceived.Contains(trimmedVerified);
            
            // Additionally verify that the license content matches the expected license type from the mapping
            bool licenseTypeMatches = VerifyLicenseType(trimmedReceived, expectedLicenseType);
            
            if (!licenseTypeMatches)
            {
                Console.WriteLine($"License type verification failed. Expected: {expectedLicenseType}");
                Console.WriteLine($"Received content snippet: {trimmedReceived[..Math.Min(200, trimmedReceived.Length)]}...");
            }
            
            return Task.FromResult(new CompareResult(contentMatches && licenseTypeMatches));
        }

        private static bool VerifyLicenseType(string licenseContent, string expectedLicenseType)
        {
            // Normalize the content for comparison
            string normalizedContent = licenseContent.ToUpperInvariant();
            
            return expectedLicenseType switch
            {
                "MIT" => normalizedContent.Contains("MIT LICENSE") || 
                         normalizedContent.Contains("THE MIT LICENSE"),
                "Apache-2.0" => normalizedContent.Contains("APACHE LICENSE") || 
                                normalizedContent.Contains("APACHE 2.0") ||
                                normalizedContent.Contains("APACHE-2.0") ||
                                normalizedContent.Contains("VERSION 2.0"),
                "BSD-3-Clause" => normalizedContent.Contains("BSD") ||
                                  normalizedContent.Contains("3-CLAUSE") ||
                                  normalizedContent.Contains("REDISTRIBUTION AND USE IN SOURCE AND BINARY FORMS"),
                "MS-PL" => normalizedContent.Contains("MICROSOFT") || 
                           normalizedContent.Contains("MS-PL") ||
                           normalizedContent.Contains("MICROSOFT PUBLIC LICENSE"),
                _ => true // If we don't have specific validation for this license type, allow it to pass
            };
        }

        private sealed class DisposableWebDriver : IDisposable
        {
            private readonly IWebDriver _driver;

            public DisposableWebDriver()
            {
                var options = new ChromeOptions();
                options.AddArguments("--disable-dev-shm-usage", "--headless");
                _driver = new ChromeDriver(options);
            }

            public void Dispose()
            {
                _driver.Close();
                _driver.Quit();
                _driver.Dispose();
            }

            internal IWebElement FindElement(By by) => _driver.FindElement(by);
            internal INavigation Navigate() => _driver.Navigate();
        }

        private sealed class DriverSlot(SemaphoreSlim sem) : IDisposable
        {
            private bool _disposed;

            public async Task WaitAsync() => await sem.WaitAsync();

            public void Dispose()
            {
                if (!_disposed)
                {
                    sem.Release();
                    _disposed = true;
                }
            }
        }
    }
}
