using NuGetUtility.ProjectFiltering;

namespace NuGetUtility.Test.ProjectFiltering
{
    [TestFixture]
    class ProjectFiltererTest
    {
        private ProjectFilterer _filterer = null!;

        [SetUp]
        public void Setup()
        {
            _filterer = new ProjectFilterer();
        }

        [TestCase("test.shproj", true)]
        [TestCase("test.SHPROJ", true)]
        [TestCase("test.csproj", false)]
        [TestCase("test.vbproj", false)]
        public void IsSharedProject_DetectsProjectTypeCorrectly(string projectPath, bool expectedResult)
        {
            bool result = _filterer.IsSharedProject(projectPath);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public void FilterProjects_ExcludesSharedProjects_WhenIncludeSharedProjectsIsFalse()
        {
            var projects = new[] { "one.csproj", "two.shproj", "three.csproj" };

            var result = _filterer.FilterProjects(projects, false).ToArray();

            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result, Does.Contain("one.csproj"));
            Assert.That(result, Does.Contain("three.csproj"));
            Assert.That(result, Does.Not.Contain("two.shproj"));
        }

        [Test]
        public void FilterProjects_IncludesAllProjects_WhenIncludeSharedProjectsIsTrue()
        {
            var projects = new[] { "one.csproj", "two.shproj", "three.csproj" };

            var result = _filterer.FilterProjects(projects, true).ToArray();

            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result, Does.Contain("one.csproj"));
            Assert.That(result, Does.Contain("two.shproj"));
            Assert.That(result, Does.Contain("three.csproj"));
        }

        [Test]
        public void Instance_Exists() {
            var instance = ProjectFilterer.Instance;

            Assert.That(instance, Is.Not.Null);
        }
    }
}
