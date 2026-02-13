// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace NuGetUtility.Wrapper.MsBuildWrapper
{
    internal class ProjectWrapper : IProject
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private const string PackageReferenceItemType = "PackageReference";
        private const string TargetFrameworkProperty = "TargetFramework";

        private readonly Project _project;

        public ProjectWrapper(Project project)
        {
            _project = project;
        }

        public bool TryGetAssetsPath([NotNullWhen(true)] out string assetsFile)
        {
            assetsFile = _project.GetPropertyValue(ProjectAssetsFile);
            if (string.IsNullOrEmpty(assetsFile))
            {
                return false;
            }

            if (!File.Exists(assetsFile))
            {
                throw new MsBuildAbstractionException(
                    $"Failed to get the project assets file for project {_project.FullPath} ({assetsFile})");
            }

            return true;
        }

        public IEnumerable<string> GetEvaluatedIncludes()
        {
            return _project.AllEvaluatedItems.Select(projectItem => projectItem.EvaluatedInclude);
        }

        public IEnumerable<PackageReferenceMetadata> GetPackageReferences()
        {
            // Read evaluated PackageReference items from the current project context.
            return _project.GetItems(PackageReferenceItemType)
                .Select(item => new PackageReferenceMetadata(item.EvaluatedInclude, CreateMetadata(item)));
        }

        public IEnumerable<PackageReferenceMetadata> GetPackageReferencesForTarget(string targetFramework)
        {
            // Re-evaluate the project for a specific target framework to read conditional references.
            Dictionary<string, string> properties = new Dictionary<string, string>(_project.GlobalProperties, StringComparer.OrdinalIgnoreCase)
            {
                [TargetFrameworkProperty] = targetFramework
            };

            Project targetProject = new Project(_project.FullPath, properties, _project.ToolsVersion, _project.ProjectCollection);

            return targetProject.GetItems(PackageReferenceItemType)
                .Select(item => new PackageReferenceMetadata(item.EvaluatedInclude, CreateMetadata(item)));
        }

        public string FullPath => _project.FullPath;

        private static IReadOnlyDictionary<string, string> CreateMetadata(ProjectItem item)
        {
            // Normalize metadata names for case-insensitive lookups (e.g., Publish).
            Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectMetadata projectMetadata in item.Metadata)
            {
                metadata[projectMetadata.Name] = projectMetadata.EvaluatedValue;
            }

            return metadata;
        }
    }
}
