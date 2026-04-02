using NuGetUtility.ProjectFiltering;

namespace NuGetUtility.Test.ProjectFiltering
{
    class ProjectFiltererTest
    {
        private ProjectFilter _filterer = null!;

        [Before(Test)]
        public void Setup()
        {
            _filterer = new ProjectFilter();
        }

        [Test]
        public async Task FilterProjects_ExcludesSharedProjects_WhenIncludeSharedProjectsIsFalse()
        {
            string[] projects = ["one.csproj", "two.shproj", "three.csproj", "four.SHPROJ"];

            string[] result = _filterer.FilterProjects(projects, false).ToArray();

            await Assert.That(result.Length).IsEqualTo(2);
            await Assert.That(result).Contains("one.csproj");
            await Assert.That(result).Contains("three.csproj");
            await Assert.That(result).DoesNotContain("two.shproj");
            await Assert.That(result).DoesNotContain("four.SHPROJ");
        }

        [Test]
        public async Task FilterProjects_IncludesAllProjects_WhenIncludeSharedProjectsIsTrue()
        {
            string[] projects = ["one.csproj", "two.shproj", "three.csproj", "four.SHPROJ"];

            string[] result = _filterer.FilterProjects(projects, true).ToArray();

            await Assert.That(result.Length).IsEqualTo(4);
            await Assert.That(result).Contains("one.csproj");
            await Assert.That(result).Contains("two.shproj");
            await Assert.That(result).Contains("three.csproj");
        }
    }
}
