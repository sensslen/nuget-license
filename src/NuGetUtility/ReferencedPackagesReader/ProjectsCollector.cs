// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NuGetUtility.Wrapper.MsBuildWrapper;

namespace NuGetUtility.ReferencedPackagesReader
{
    public class ProjectsCollector
    {
        private readonly IMsBuildAbstraction _msBuild;
        public ProjectsCollector(IMsBuildAbstraction msBuild)
        {
            _msBuild = msBuild;
        }

        public async Task<IEnumerable<string>> GetProjectsAsync(string inputPath)
        {
            return Path.GetExtension(inputPath).StartsWith(".sln")
                ? (await _msBuild.GetProjectsFromSolutionAsync(Path.GetFullPath(inputPath))).Where(File.Exists).Select(Path.GetFullPath)
                : new[] { Path.GetFullPath(inputPath) };
        }
    }
}
