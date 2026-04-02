// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NSubstitute;
using NuGetUtility.Extensions;
using NuGetUtility.Wrapper.MsBuildWrapper;

namespace NuGetUtility.Test.Extensions
{
    public class ProjectExtensionsTest
    {
        [Before(Test)]
        public void SetUp()
        {
            _project = Substitute.For<IProject>();
        }

        private IProject _project = null!;

        [Test]
        public async Task GetPackagesConfigPath_Should_Return_CorrectPath()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            _project.FullPath.Returns(path);

            string result = _project.GetPackagesConfigPath();

            await Assert.That(result).IsEqualTo(Path.Combine(Path.GetDirectoryName(path)!, "packages.config"));
        }

        [Arguments(new string?[] { }, false)]
        [Arguments(new string?[] { null }, false)]
        [Arguments(new string?[] { null, "not-packages.config" }, false)]
        [Arguments(new string?[] { "not-packages.config" }, false)]
        [Arguments(new string?[] { "packages.config" }, true)]
        [Arguments(new string?[] { null, "packages.config" }, true)]
        [Arguments(new string?[] { "not-packages.config", "packages.config" }, true)]
        [Arguments(new string?[] { null, "not-packages.config", "packages.config" }, true)]
        [Test]
        public async Task HasPackagesConfigFile_Should_Return_Correct_Result(IEnumerable<string> evaluatedIncludes, bool expectation)
        {
            _project.GetEvaluatedIncludes().Returns(evaluatedIncludes);

            await Assert.That(_project.HasPackagesConfigFile()).IsEqualTo(expectation);
        }
    }
}
