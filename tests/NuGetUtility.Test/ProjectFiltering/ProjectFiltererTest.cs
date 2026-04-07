// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.ProjectFiltering;

namespace NuGetUtility.Test.ProjectFiltering
{
    class ProjectFiltererTest
    {
        private readonly ProjectFilter _filterer;

        public ProjectFiltererTest()
        {
            _filterer = new ProjectFilter();
        }

        [Test]
        public async Task FilterProjects_ExcludesSharedProjects_WhenIncludeSharedProjectsIsFalse()
        {
            string[] projects = ["one.csproj", "two.shproj", "three.csproj", "four.SHPROJ"];

            string[] result = _filterer.FilterProjects(projects, false).ToArray();

            await Assert.That(result.Count).IsEqualTo(2);
            await Assert.That(result.Contains("one.csproj")).IsTrue();
            await Assert.That(result.Contains("three.csproj")).IsTrue();
            await Assert.That(result.Contains("two.shproj")).IsFalse();
            await Assert.That(result.Contains("four.SHPROJ")).IsFalse();
        }

        [Test]
        public async Task FilterProjects_IncludesAllProjects_WhenIncludeSharedProjectsIsTrue()
        {
            string[] projects = ["one.csproj", "two.shproj", "three.csproj", "four.SHPROJ"];

            string[] result = _filterer.FilterProjects(projects, true).ToArray();

            await Assert.That(result.Count).IsEqualTo(4);
            await Assert.That(result.Contains("one.csproj")).IsTrue();
            await Assert.That(result.Contains("two.shproj")).IsTrue();
            await Assert.That(result.Contains("three.csproj")).IsTrue();
        }
    }
}
