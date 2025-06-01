using FeatBit.EvaluationServer.Hub.Domain.Evaluation;

namespace FeatBit.EvaluationServer.Hub.Infrastructure.Evaluation;

public class RuleEvaluator : IRuleEvaluator
{
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
            new { Attribute = "country", Operator = "Equal", Value = "US" },
            new { Attribute = "age", Operator = "BiggerThan", Value = "18" }
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
        // Simple condition evaluation logic
        // In a real implementation, this would be more sophisticated
        return op switch
        {
            "Equal" => actual.ToString() == expected,
            "BiggerThan" => decimal.TryParse(actual.ToString(), out var actualNum) &&
                           decimal.TryParse(expected, out var expectedNum) &&
                           actualNum > expectedNum,
            _ => false
        };
    }
} 