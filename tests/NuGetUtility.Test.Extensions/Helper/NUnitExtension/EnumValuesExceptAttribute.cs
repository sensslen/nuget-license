// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace NuGetUtility.Test.Extensions.Helper.NUnitExtension
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class EnumValuesExceptAttribute : NUnitAttribute, IParameterDataSource
    {
        private readonly object[] _exceptions;

        public EnumValuesExceptAttribute(params object[] exceptions)
        {
            _exceptions = exceptions;
        }

        public IEnumerable GetData(IParameterInfo parameter)
        {
            return new EnumEnumerableWithException(parameter.ParameterType, _exceptions);
        }
    }
}
