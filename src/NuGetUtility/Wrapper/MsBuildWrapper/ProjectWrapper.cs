// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    internal class ProjectWrapper(Project project) : IProject
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private const string PackageReferenceItemType = "PackageReference";
        private const string TargetFrameworkProperty = "TargetFramework";

        public bool TryGetAssetsPath([NotNullWhen(true)] out string assetsFile)
        {
            assetsFile = project.GetPropertyValue(ProjectAssetsFile);
            if (string.IsNullOrEmpty(assetsFile))
            {
                return false;
            }

            if (!File.Exists(assetsFile))
            {
                throw new MsBuildAbstractionException(
                    $"Failed to get the project assets file for project {project.FullPath} ({assetsFile})");
            }

            return true;
        }

        public IEnumerable<string> GetEvaluatedIncludes()
        {
            return project.AllEvaluatedItems.Select(projectItem => projectItem.EvaluatedInclude);
        }

        public IEnumerable<PackageReferenceMetadata> GetPackageReferences()
        {
            // Read evaluated PackageReference items from the current project context.
            return project.GetItems(PackageReferenceItemType)
                .Select(item => new PackageReferenceMetadata(item.EvaluatedInclude, CreateMetadata(item)));
        }

        public IEnumerable<PackageReferenceMetadata> GetPackageReferencesForTarget(string targetFramework)
        {
            // Re-evaluate the project for a specific target framework to read conditional references.
            Dictionary<string, string> properties = new(project.GlobalProperties, StringComparer.OrdinalIgnoreCase)
            {
                [TargetFrameworkProperty] = targetFramework
            };

            using ProjectCollection projectCollection = new();
            Project targetProject = new(project.FullPath, properties, project.ToolsVersion, projectCollection);
            try
            {
                return targetProject.GetItems(PackageReferenceItemType)
                    .Select(item => new PackageReferenceMetadata(item.EvaluatedInclude, CreateMetadata(item)))
                    .ToList();
            }
            finally
            {
                projectCollection.UnloadProject(targetProject);
            }
        }

        public string FullPath => project.FullPath;

        private static IReadOnlyDictionary<string, string> CreateMetadata(ProjectItem item)
        {
            // Normalize metadata names for case-insensitive lookups (e.g., Publish).
            Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectMetadata projectMetadata in item.Metadata)
            {
                metadata[projectMetadata.Name] = projectMetadata.EvaluatedValue;
            }

            return metadata;
        }
    }
}
