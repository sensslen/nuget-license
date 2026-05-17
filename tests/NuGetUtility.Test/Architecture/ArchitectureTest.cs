// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NetArchTest.Rules;

namespace NuGetUtility.Test.Architecture
{
    public abstract class ArchitectureTest
    {
        protected Types Types { get; } = Types.InAssemblies([System.Reflection.Assembly.Load(AssemblyNames.NuGetUtility)]);

        internal static class AssemblyNames
        {
            internal const string NuGetUtility = "NugetUtility";
        }
    }
}
