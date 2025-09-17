// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Reflection;
using NetArchTest.Rules;

namespace NuGetUtility.Test.Architecture
{
    public abstract class ArchitectureTest
    {
        protected Types Types { get; }

        protected ArchitectureTest()
        {
            Types = Types.InAssemblies([Assembly.Load(AssemblyNames.NuGetUtility)]);
        }

        internal static class AssemblyNames
        {
            internal const string NuGetUtility = "NugetUtility";
        }
    }
}
