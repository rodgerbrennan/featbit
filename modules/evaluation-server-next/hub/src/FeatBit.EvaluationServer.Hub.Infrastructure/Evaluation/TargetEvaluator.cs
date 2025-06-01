using System.Text.RegularExpressions;
using FeatBit.EvaluationServer.Hub.Domain.Evaluation;

namespace FeatBit.EvaluationServer.Hub.Infrastructure.Evaluation;

public class TargetEvaluator : ITargetEvaluator
{
    public Task<bool> EvaluateAsync(string targetKey, string userId, IDictionary<string, object> userAttributes)
    {
        // If no user attributes, only match on userId
        if (userAttributes == null || !userAttributes.Any())
        {
            return Task.FromResult(targetKey == userId);
        }

        // Check if target key matches any user attribute
        foreach (var (key, value) in userAttributes)
        {
            if (IsMatch(targetKey, key, value))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private static bool IsMatch(string targetKey, string attributeKey, object attributeValue)
    {
        // Direct key match
        if (targetKey == attributeKey)
        {
            return true;
        }

        // Handle different attribute value types
        return attributeValue switch
        {
            string strValue => IsStringMatch(targetKey, strValue),
            int intValue => targetKey == intValue.ToString(),
            bool boolValue => targetKey == boolValue.ToString().ToLower(),
            double doubleValue => targetKey == doubleValue.ToString("G"),
            // Add more type handling as needed
            _ => false
        };
    }

    private static bool IsStringMatch(string targetKey, string value)
    {
        // Exact match
        if (targetKey == value)
        {
            return true;
        }

        // Pattern match (simple wildcard support)
        if (targetKey.Contains('*'))
        {
            var pattern = "^" + Regex.Escape(targetKey).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase);
        }

        return false;
    }
} 