// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NetArchTest.Rules;

namespace NuGetUtility.Test.Architecture
{
    internal static class ConditionsExtensions
    {
        public static void Assert(this ConditionList conditions, string message = "Architecture rule broken.")
        {
            TestResult ruleResult = conditions.GetResult();
            string failingTypeNames = string.Join(Environment.NewLine, ruleResult.FailingTypeNames ?? Array.Empty<string>());
            NUnit.Framework.Assert.That(ruleResult.IsSuccessful, Is.True, $"{message}{Environment.NewLine}Offending types:{Environment.NewLine}{failingTypeNames}");
        }
    }
}
