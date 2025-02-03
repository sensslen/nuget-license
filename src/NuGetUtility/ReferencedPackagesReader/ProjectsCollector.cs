﻿// Licensed to the projects contributors.
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

        public IEnumerable<string> GetProjects(string inputPath)
        {
            return Path.GetExtension(inputPath).StartsWith(".sln")
                ? _msBuild.GetProjectsFromSolution(Path.GetFullPath(inputPath)).Where(File.Exists).Select(Path.GetFullPath)
                : new[] { Path.GetFullPath(inputPath) };
        }
    }
}
