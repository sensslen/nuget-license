// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace NuGetUtility.Wrapper.SolutionPersistenceWrapper
{
    public class SolutionPersistanceWrapper : ISolutionPersistanceWrapper
    {
        public async Task<IEnumerable<string>> GetProjectsFromSolutionAsync(string inputPath)
        {
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(inputPath) ?? throw new SolutionPersistanceException("Failed to determine serializer for solution");

            Microsoft.VisualStudio.SolutionPersistence.Model.SolutionModel model = await serializer.OpenAsync(inputPath, CancellationToken.None);
            string? solutionPath = Path.GetDirectoryName(inputPath);
            return model.SolutionProjects.Select(p => solutionPath is null ? p.FilePath : Path.Combine(solutionPath, p.FilePath));
        }
    }
}
