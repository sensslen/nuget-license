// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.LicenseValidator;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace NuGetUtility.Test.LicenseValidator
{
    [TestFixture]
    public class UrlToLicenseMappingTest
    {
        private const int RETRY_COUNT = 3;
        private DisposableWebDriver _driver = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            _driver = new DisposableWebDriver();
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            _driver.Dispose();
        }

        [TestCaseSource(typeof(UrlToLicenseMapping), nameof(UrlToLicenseMapping.Default))]
        public async Task License_Should_Be_Available_And_Match_Expected_License(KeyValuePair<Uri, string> mappedValue)
        {
            int retryCount = 0;
            int baseDelayMs = 2000;
            while (true)
            {
                Result<string> licenseResult = await GetLicenseValue(mappedValue.Key);
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
                await TestContext.Out.WriteLineAsync($"Failed to check license. Retry count: {retryCount}\n\n");
                await TestContext.Out.WriteLineAsync($"Error:");
                await TestContext.Out.WriteLineAsync(licenseResult.Error);
                await TestContext.Out.WriteLineAsync($"\n\nRetrying after {retryTimeout}ms\n\n");
                await Task.Delay(retryTimeout);
            }
        }

        private async Task<Result<string>> GetLicenseValue(Uri licenseUrl)
        {
            string bodyText;
            try
            {
                await _driver.Navigate().GoToUrlAsync(licenseUrl.ToString());
                bodyText = _driver.FindElement(By.TagName("body")).Text;
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
                var options = new FirefoxOptions();
                options.AddArguments("--headless");
                _driver = new FirefoxDriver(options);
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
