// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.ProjectFiltering;

namespace NuGetUtility.Test.ProjectFiltering
{
    class ProjectFiltererTest
    {
        private readonly ProjectFilter _filterer = new();

        [Test]
        [Arguments("one.csproj", false, false)]
        [Arguments("two.shproj", true, false)]
        [Arguments("three.csproj", false, false)]
        [Arguments("four.SHPROJ", true, false)]
        [Arguments("one.csproj", true, true)]
        [Arguments("two.shproj", true, true)]
        [Arguments("three.csproj", true, true)]
        [Arguments("four.SHPROJ", true, true)]
        public async Task FilterProjects_ExcludesSharedProjects_WhenIncludeSharedProjectsIsFalse(string project, bool isFiltered, bool includeSharedProjects)
        {
            string[] result = _filterer.FilterProjects([project], includeSharedProjects).ToArray();

            await Assert.That(result).Count().IsEqualTo(isFiltered ? 0 : 1);
            if (!isFiltered)
            {
                await Assert.That(result).Contains(project);
            }
        }
    }
}
