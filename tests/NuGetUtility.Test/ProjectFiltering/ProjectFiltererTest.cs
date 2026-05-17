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
        [Arguments("one.csproj", false, true)]
        [Arguments("two.shproj", false, true)]
        [Arguments("three.csproj", false, true)]
        [Arguments("four.SHPROJ", false, true)]
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
