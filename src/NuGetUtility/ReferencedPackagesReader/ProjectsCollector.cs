// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ProjectsCollector
    {
        private readonly ISolutionPersistanceWrapper _solutionPersistance;
        private readonly IFileSystem _fileSystem;

        public ProjectsCollector(ISolutionPersistanceWrapper solutionPersistance, IFileSystem fileSystem)
        {
            _solutionPersistance = solutionPersistance;
            _fileSystem = fileSystem;
        }

        public async Task<IEnumerable<string>> GetProjectsAsync(string inputPath)
        {
            return _fileSystem.Path.GetExtension(inputPath).StartsWith(".sln")
                ? (await _solutionPersistance.GetProjectsFromSolutionAsync(_fileSystem.Path.GetFullPath(inputPath))).Where(_fileSystem.File.Exists).Select(_fileSystem.Path.GetFullPath)
                : [_fileSystem.Path.GetFullPath(inputPath)];
        }
    }
}
