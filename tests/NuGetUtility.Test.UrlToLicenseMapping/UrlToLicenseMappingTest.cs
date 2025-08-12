// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.LicenseValidator;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace NuGetUtility.Test.LicenseValidator
{
    public static class UrlToLicenseMappingTestSource
    {
        public static IEnumerable<KeyValuePair<Uri, string>> GetDefaultMappings()
        {
            return UrlToLicenseMapping.Default;
        }
    }

    public class UrlToLicenseMappingTest
    {
        private const int RETRY_COUNT = 3;
        [Test]
        [MethodDataSource(typeof(UrlToLicenseMappingTestSource), nameof(UrlToLicenseMappingTestSource.GetDefaultMappings))]
        [NotInParallel(nameof(License_Should_Be_Available_And_Match_Expected_License))]
        public async Task License_Should_Be_Available_And_Match_Expected_License(KeyValuePair<Uri, string> mappedValue)
        {
            int retryCount = 0;
            int baseDelayMs = 2000;
            using var driver = new DisposableWebDriver();
            while (true)
            {
                Result<string> licenseResult = await GetLicenseValue(mappedValue.Key, driver);
                if (licenseResult.IsSuccess)
                {
                    await Verify(licenseResult.Value).HashParameters().UseStringComparer(CompareLicense);
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
            if (bodyText.Contains("rate limit"))
            {
                return new() { Error = $"Rate limit exceeded:\n{bodyText}" };
            }
            return new() { Value = bodyText };
        }

        private static Task<CompareResult> CompareLicense(string received, string verified, IReadOnlyDictionary<string, object> context)
        {
            return Task.FromResult(new CompareResult((!string.IsNullOrWhiteSpace(verified)) && received.Contains(verified)));
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
    }
}
