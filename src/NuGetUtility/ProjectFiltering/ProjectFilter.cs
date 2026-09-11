// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.ProjectFiltering
{
    public static class ProjectFilter
    {
        public static IEnumerable<string> FilterProjects(IEnumerable<string> projects, bool includeSharedProjects)
        {
            return includeSharedProjects ? projects : projects.Where(p => !IsSharedProject(p));
        }

        private static bool IsSharedProject(string projectPath)
        {
            return projectPath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
