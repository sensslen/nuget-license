// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.ProjectFiltering
{
    public class ProjectFilter
    {

        /// <summary>
        /// Filters a collection of project paths based on inclusion rules.
        /// </summary>
        /// <param name="projects">Collection of project paths to filter</param>
        /// <param name="includeSharedProjects">Whether to include .shproj files</param>
        /// <returns>Filtered collection of project paths</returns>
        public IEnumerable<string> FilterProjects(IEnumerable<string> projects, bool includeSharedProjects)
        {
            return includeSharedProjects ? projects : projects.Where(p => !IsSharedProject(p));
        }

        /// <summary>
        /// Determines if a project is a shared project based on file extension.
        /// </summary>
        /// <param name="projectPath">Path to the project file</param>
        /// <returns>True if the project is a shared project, otherwise false</returns>
        private static bool IsSharedProject(string projectPath)
        {
            return projectPath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
