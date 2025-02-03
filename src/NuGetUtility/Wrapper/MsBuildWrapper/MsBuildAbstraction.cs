// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    public class MsBuildAbstraction : IMsBuildAbstraction
    {
        private ProjectCollection? _projects;

        private ProjectCollection Projects => _projects ??= InitializeProjectCollection();

        public MsBuildAbstraction()
        {
            RegisterMsBuildLocatorIfNeeded();
        }

        public IProject GetProject(string projectPath)
        {
#if !NETFRAMEWORK
            if (projectPath.EndsWith("vcxproj"))
            {
                throw new MsBuildAbstractionException($"Please use the .net Framework version to analyze c++ projects (Project: {projectPath})");
            }
#endif

            Project project = Projects.LoadProject(projectPath);

            return new ProjectWrapper(project);
        }

        public async Task<IEnumerable<string>> GetProjectsFromSolutionAsync(string inputPath)
        {
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(inputPath) ?? throw new MsBuildAbstractionException("Failed to determine serializer for solution");

            Microsoft.VisualStudio.SolutionPersistence.Model.SolutionModel model = await serializer.OpenAsync(inputPath, CancellationToken.None);
            return model.SolutionProjects.Select(p => p.FilePath);
        }

        private static void RegisterMsBuildLocatorIfNeeded()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }

        private static ProjectCollection InitializeProjectCollection()
        {
            ProjectCollection collection = ProjectCollection.GlobalProjectCollection;
            collection.UnloadAllProjects();
            return collection;
        }
    }
}
