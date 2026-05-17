// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.IO.Abstractions;
using NuGetUtility.Wrapper.SolutionPersistenceWrapper;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ProjectsCollector(ISolutionPersistenceWrapper solutionPersistence, IFileSystem fileSystem)
    {
        public async Task<IEnumerable<string>> GetProjectsAsync(string inputPath)
        {
            var extension = Path.GetExtension(inputPath);
            return new[] { ".sln", ".slnx" }.Contains(extension)
                ? (await solutionPersistence.GetProjectsFromSolutionAsync(fileSystem.Path.GetFullPath(inputPath))).Where(fileSystem.File.Exists).Select(fileSystem.Path.GetFullPath)
                : [fileSystem.Path.GetFullPath(inputPath)];
        }
    }
}
