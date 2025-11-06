// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    public class MsBuildAbstraction : IMsBuildAbstraction
    {
        private ProjectCollection Projects { get; }

        public MsBuildAbstraction()
        {
            RegisterMsBuildLocatorIfNeeded();
            Projects = InitializeProjectCollection();
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
