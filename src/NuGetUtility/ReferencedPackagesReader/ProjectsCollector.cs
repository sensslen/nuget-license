// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ProjectsCollector
    {
        private readonly ISolutionPersistanceWrapper _solutionPersistance;
        public ProjectsCollector(ISolutionPersistanceWrapper solutionPersistance)
        {
            _solutionPersistance = solutionPersistance;
        }

        public async Task<IEnumerable<string>> GetProjectsAsync(string inputPath)
        {
            return Path.GetExtension(inputPath).StartsWith(".sln")
                ? (await _solutionPersistance.GetProjectsFromSolutionAsync(Path.GetFullPath(inputPath))).Where(File.Exists).Select(Path.GetFullPath)
                : [Path.GetFullPath(inputPath)];
        }
    }
}
