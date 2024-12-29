// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using AutoFixture.Kernel;
using NuGetUtility.Wrapper.NuGetWrapper.Versioning;

namespace NuGetUtility.Test.Helper.AutoFixture.NuGet.Versioning
{
    internal class NuGetVersionBuilder : ISpecimenBuilder
    {
        private readonly Random _rnd = new Random();

        public object Create(object request, ISpecimenContext context)
        {
            if (request is System.Type t && t == typeof(INuGetVersion))
            {
                return new NuGetVersion($"{_rnd.Next(100, 999)}.{_rnd.Next(100, 999)}{GetPatch()}{GetBeta()}");
            }

            return new NoSpecimen();
        }

        private string GetBeta()
        {
            return RandomBool() ? $"-beta.{_rnd.Next(100, 999)}" : "";
        }

        private string GetPatch()
        {
            return RandomBool() ? $".{_rnd.Next(100, 999)}" : "";
        }

        private bool RandomBool()
        {
            return (_rnd.Next() % 2) == 0;
        }

        private class NuGetVersion : INuGetVersion
        {
            private readonly string _version;

            public NuGetVersion(string version)
            {
                _version = version;
            }

            public int CompareTo(INuGetVersion? other) => throw new NotImplementedException();

            public override string ToString()
            {
                return _version;
            }
        }
    }
}
