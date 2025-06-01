using System.Text.RegularExpressions;

namespace FeatBit.EvaluationServer.Hub.Evaluation;

public class RuleEvaluator : IRuleEvaluator
{
    private const string LessThan = "LessThan";
    private const string BiggerThan = "BiggerThan";
    private const string LessEqualThan = "LessEqualThan";
    private const string BiggerEqualThan = "BiggerEqualThan";
    private const string Equal = "Equal";
    private const string NotEqual = "NotEqual";
    private const string Contains = "Contains";
    private const string NotContain = "NotContain";
    private const string StartsWith = "StartsWith";
    private const string EndsWith = "EndsWith";
    private const string MatchRegex = "MatchRegex";
    private const string NotMatchRegex = "NotMatchRegex";
    private const string IsOneOf = "IsOneOf";
    private const string NotOneOf = "NotOneOf";
    private const string IsTrue = "IsTrue";
    private const string IsFalse = "IsFalse";

    public Task<bool> EvaluateAsync(Guid ruleId, IDictionary<string, object> userAttributes)
    {
        // For now, we'll implement a simple rule evaluation
        // In a real implementation, we would fetch the rule from a store using ruleId
        
        if (userAttributes == null || !userAttributes.Any())
        {
            return Task.FromResult(false);
        }

        // Simulate rule conditions (in real implementation, these would come from the rule definition)
        var conditions = new[]
        {
            new { Attribute = "country", Operator = Equal, Value = "US" },
            new { Attribute = "age", Operator = BiggerThan, Value = "18" }
        };

        // All conditions must match for the rule to match
        foreach (var condition in conditions)
        {
            if (!userAttributes.TryGetValue(condition.Attribute, out var attributeValue))
            {
                return Task.FromResult(false);
            }

            if (!EvaluateCondition(condition.Operator, attributeValue, condition.Value))
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    private static bool EvaluateCondition(string op, object actual, string expected)
    {
        return op switch
        {
            Equal => actual.ToString() == expected,
            NotEqual => actual.ToString() != expected,
            Contains => actual.ToString().Contains(expected),
            NotContain => !actual.ToString().Contains(expected),
            StartsWith => actual.ToString().StartsWith(expected),
            EndsWith => actual.ToString().EndsWith(expected),
            MatchRegex => Regex.IsMatch(actual.ToString(), expected),
            NotMatchRegex => !Regex.IsMatch(actual.ToString(), expected),
            IsOneOf => expected.Split(',').Contains(actual.ToString()),
            NotOneOf => !expected.Split(',').Contains(actual.ToString()),
            IsTrue => bool.TryParse(actual.ToString(), out var b) && b,
            IsFalse => bool.TryParse(actual.ToString(), out var b) && !b,
            LessThan => CompareNumeric(actual, expected) < 0,
            BiggerThan => CompareNumeric(actual, expected) > 0,
            LessEqualThan => CompareNumeric(actual, expected) <= 0,
            BiggerEqualThan => CompareNumeric(actual, expected) >= 0,
            _ => false
        };
    }

    private static int CompareNumeric(object actual, string expected)
    {
        if (double.TryParse(actual.ToString(), out var actualNum) && 
            double.TryParse(expected, out var expectedNum))
        {
            return actualNum.CompareTo(expectedNum);
        }
        return 0;
    }
} 