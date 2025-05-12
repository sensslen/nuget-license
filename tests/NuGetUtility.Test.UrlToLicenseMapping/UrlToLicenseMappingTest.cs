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
            while (retryCount < RETRY_COUNT)
            {
                try
                {
                    Result<string> licenseResult = await GetLicenseValue(mappedValue.Key);
                    if (licenseResult.IsSuccess)
                    {
                        await Verify(licenseResult.Value).HashParameters().UseStringComparer(CompareLicense);
                        return;
                    }
                    await Task.Delay(500 + Random.Shared.Next(5000));
                    await TestContext.Out.WriteLineAsync(licenseResult.Error);
                }
                catch (WebDriverException e)
                {
                    retryCount++;
                    await TestContext.Out.WriteLineAsync(e.ToString());
                }
                await TestContext.Out.WriteLineAsync($"Failed to check license for the {retryCount} time - retrying");
            }
            Assert.Fail($"Failed to check license for {mappedValue.Key} after {RETRY_COUNT} attempts.");
        }

        private async Task<Result<string>> GetLicenseValue(Uri licenseUrl)
        {
            await _driver.Navigate().GoToUrlAsync(licenseUrl.ToString());
            string bodyText = _driver.FindElement(By.TagName("body")).Text;
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
