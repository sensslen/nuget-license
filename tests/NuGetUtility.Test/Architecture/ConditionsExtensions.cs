// Licensed to the project contributors.
// The license conditions are provided in the LICENSE file located in the project root

using NetArchTest.Rules;

namespace NuGetUtility.Test.Architecture
{
    internal static class ConditionsExtensions
    {
        public static Task Assert(this ConditionList conditions, string message = "Architecture rule broken.")
        {
            NetArchTest.Rules.TestResult ruleResult = conditions.GetResult();
            if (ruleResult.IsSuccessful)
            {
                return Task.CompletedTask;
            }

            string failingTypeNames = string.Join(Environment.NewLine, ruleResult.FailingTypeNames ?? Array.Empty<string>());
            throw new Exception($"{message}{Environment.NewLine}Offending types:{Environment.NewLine}{failingTypeNames}");
        }
    }
}
