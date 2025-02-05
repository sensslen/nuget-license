// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace NuGetUtility.Wrapper.SolutionPersistenceWrapper
{
    public interface ISolutionPersistanceWrapper
    {
        Task<IEnumerable<string>> GetProjectsFromSolutionAsync(string inputPath);
    }
}
