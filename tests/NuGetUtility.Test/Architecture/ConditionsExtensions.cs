// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NetArchTest.Rules;
using TUnit.Assertions.AssertConditions.Throws;

namespace NuGetUtility.Test.Architecture
{
    internal static class ConditionsExtensions
    {
        public static async Task Assert(this ConditionList conditions, string message = "Architecture rule broken.")
        {
            TestResult ruleResult = conditions.GetResult();
            string failingTypeNames = string.Join(Environment.NewLine, ruleResult.FailingTypeNames ?? Array.Empty<string>());
            await TUnit.Assertions.Assert.That(ruleResult.IsSuccessful).IsTrue().Because($"{message}{Environment.NewLine}Offending types:{Environment.NewLine}{failingTypeNames}");
        }
    }
}
