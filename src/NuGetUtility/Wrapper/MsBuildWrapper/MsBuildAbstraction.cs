// Licensed to the project contributors.
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
#if NETFRAMEWORK
                VisualStudioInstance? instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(i => i.Version).FirstOrDefault();
                if (instance is null)
                {
                    throw new MsBuildAbstractionException(
                        "No MSBuild instance could be detected. The .NET Framework variant of this tool requires Visual Studio or the Visual Studio Build Tools to be installed in order to locate MSBuild. " +
                        "Please install the \"Visual Studio Build Tools\" (https://visualstudio.microsoft.com/downloads/?q=build+tools) or use the .NET Core (dotnet tool) variant instead. " +
                        "See https://learn.microsoft.com/visualstudio/msbuild/updating-an-existing-application for more information on how MSBuild is detected.");
                }

                MSBuildLocator.RegisterInstance(instance);
#else
                MSBuildLocator.RegisterInstance(MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(i => i.Version).First());
#endif
            }
        }

        private ProjectCollection GetProjectCollection()
        {
            if (_projects is null)
            {
                _projects = InitializeProjectCollection();
            }

            return _projects;
        }

        private static ProjectCollection InitializeProjectCollection()
        {
            ProjectCollection collection = ProjectCollection.GlobalProjectCollection;
            collection.UnloadAllProjects();
            return collection;
        }
    }
}
