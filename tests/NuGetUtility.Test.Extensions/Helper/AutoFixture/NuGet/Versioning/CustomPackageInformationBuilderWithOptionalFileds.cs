// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture;
using AutoFixture.Kernel;
using NuGetUtility.PackageInformationReader;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Test.Extensions.Helper.AutoFixture.NuGet.Versioning
{
    public class CustomPackageInformationBuilderWithOptionalFileds : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is System.Type t && t == typeof(CustomPackageInformation))
            {
                return new CustomPackageInformation(context.Create<string>(),
                                                    context.Create<INuGetVersion>(),
                                                    context.Create<string>(),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<string>(context),
                                                    CreateOptional<Uri>(context));
            }

            return new NoSpecimen();
        }

        private static T? CreateOptional<T>(ISpecimenContext context) where T : notnull
        {
            return context.Create<bool>() ? context.Create<T>() : default;
        }
    }
}
