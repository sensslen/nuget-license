// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    public class MsBuildAbstraction : IMsBuildAbstraction
    {
        private ProjectCollection? _projects;

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

            Project project = GetProjectCollection().LoadProject(projectPath);

            return new ProjectWrapper(project);
        }

        private static void RegisterMsBuildLocatorIfNeeded()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }

        private static ProjectCollection GetProjectCollection()
        {
            return _projects ??= InitializeProjectCollection();
        } 

        private static ProjectCollection InitializeProjectCollection()
        {
            ProjectCollection collection = ProjectCollection.GlobalProjectCollection;
            collection.UnloadAllProjects();
            return collection;
        }
    }
}
