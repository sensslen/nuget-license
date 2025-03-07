using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetUtility.ProjectFiltering
{
    public class ProjectFilterer
    {
        /// <summary>
        /// A singleton instance of this class.
        /// </summary>
        public static ProjectFilterer Instance { get; } = new ProjectFilterer();

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
        public bool IsSharedProject(string projectPath)
        {
            return projectPath.EndsWith(".shproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
