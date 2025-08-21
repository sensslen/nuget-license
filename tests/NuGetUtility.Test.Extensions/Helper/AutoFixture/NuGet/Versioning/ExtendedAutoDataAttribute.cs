// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using AutoFixture.NUnit4;

namespace NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ExtendedAutoDataAttribute : AutoDataAttribute
    {
        public ExtendedAutoDataAttribute(params System.Type[] customizations)
            : base(() => new Fixture().AddCustomizations(customizations)) { }
    }
}
