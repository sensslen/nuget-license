﻿// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using AutoFixture.NUnit4;

namespace NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ExtendedInlineAutoDataAttribute : InlineAutoDataAttribute
    {
        public ExtendedInlineAutoDataAttribute(System.Type customization, params object[] arguments)
            : base(() => new Fixture().AddCustomizations(customization), arguments) { }
        public ExtendedInlineAutoDataAttribute(System.Type[] customizations, params object[] arguments)
            : base(() => new Fixture().AddCustomizations(customizations), arguments) { }
    }
}
