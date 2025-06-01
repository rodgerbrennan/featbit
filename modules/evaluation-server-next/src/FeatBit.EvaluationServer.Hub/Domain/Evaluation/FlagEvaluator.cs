using System.Security.Cryptography;
using System.Text;

namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public class FlagEvaluator : IFlagEvaluator
{
    public EvaluationResult Evaluate(Flag flag, Target target)
    {
        if (flag == null)
        {
            return new EvaluationResult
            {
                ErrorCode = "FLAG_NOT_FOUND",
                ErrorMessage = "Flag not found"
            };
        }

        if (!flag.IsEnabled)
        {
            return EvaluateDisabledFlag(flag);
        }

        // Check rules
        foreach (var rule in flag.Rules.Where(r => r.IsEnabled))
        {
            if (MatchRule(rule, target))
            {
                return EvaluateRule(flag, rule, target);
            }
        }

        // No rules matched, use default rule
        return EvaluateDefaultRule(flag, target);
    }

    private static EvaluationResult EvaluateDisabledFlag(Flag flag)
    {
        if (!flag.DisabledVariationEnabled || flag.DisabledVariationId == null)
        {
            return new EvaluationResult
            {
                FlagKey = flag.Key,
                Value = string.Empty,
                VariationId = string.Empty,
                VariationName = string.Empty,
                Kind = flag.VariationType,
                Reason = "DISABLED",
                Version = flag.Version
            };
        }

        var variation = flag.Variations.FirstOrDefault(v => v.Id == flag.DisabledVariationId);
        if (variation == null)
        {
            return new EvaluationResult
            {
                FlagKey = flag.Key,
                ErrorCode = "VARIATION_NOT_FOUND",
                ErrorMessage = "Disabled variation not found"
            };
        }

        return new EvaluationResult
        {
            FlagKey = flag.Key,
            Value = variation.Value,
            VariationId = variation.Id.ToString(),
            VariationName = variation.Name,
            Kind = flag.VariationType,
            Reason = "DISABLED_VARIATION",
            Version = flag.Version
        };
    }

    private static bool MatchRule(Rule rule, Target target)
    {
        foreach (var condition in rule.Conditions)
        {
            var targetValue = GetTargetValue(target, condition.Property);
            if (!MatchCondition(condition, targetValue))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetTargetValue(Target target, string property)
    {
        return property.ToLower() switch
        {
            "key" => target.Key,
            "name" => target.Name,
            _ => target.Custom.TryGetValue(property, out var value) ? value : string.Empty
        };
    }

    private static bool MatchCondition(Condition condition, string targetValue)
    {
        return condition.Op.ToLower() switch
        {
            "equals" => targetValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            "notequals" => !targetValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            "contains" => targetValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            "notcontains" => !targetValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
            "startswith" => targetValue.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            "endswith" => targetValue.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
            "matches" => targetValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
            "in" => condition.Value.Split(',').Contains(targetValue, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static EvaluationResult EvaluateRule(Flag flag, Rule rule, Target target)
    {
        if (rule.Distributions.Count == 0)
        {
            return new EvaluationResult
            {
                FlagKey = flag.Key,
                ErrorCode = "NO_VARIATION_IN_RULE",
                ErrorMessage = "No variation in rule"
            };
        }

        var bucketBy = target.Key;
        var seed = $"{flag.Key}:{rule.Id}:{bucketBy}";
        var bucket = GetBucket(seed);

        var rolloutSum = 0.0;
        foreach (var distribution in rule.Distributions)
        {
            rolloutSum += distribution.Percentage;
            if (bucket < rolloutSum)
            {
                var variation = flag.Variations.FirstOrDefault(v => v.Id == distribution.VariationId);
                if (variation == null)
                {
                    return new EvaluationResult
                    {
                        FlagKey = flag.Key,
                        ErrorCode = "VARIATION_NOT_FOUND",
                        ErrorMessage = "Variation not found"
                    };
                }

                return new EvaluationResult
                {
                    FlagKey = flag.Key,
                    Value = variation.Value,
                    VariationId = variation.Id.ToString(),
                    VariationName = variation.Name,
                    Kind = flag.VariationType,
                    Reason = "RULE_MATCH",
                    Version = flag.Version
                };
            }
        }

        return new EvaluationResult
        {
            FlagKey = flag.Key,
            ErrorCode = "NO_VARIATION_SELECTED",
            ErrorMessage = "No variation selected"
        };
    }

    private static EvaluationResult EvaluateDefaultRule(Flag flag, Target target)
    {
        if (flag.Variations.Count == 0)
        {
            return new EvaluationResult
            {
                FlagKey = flag.Key,
                ErrorCode = "NO_VARIATION_IN_FLAG",
                ErrorMessage = "No variation in flag"
            };
        }

        // Use first variation as default
        var variation = flag.Variations.First();
        return new EvaluationResult
        {
            FlagKey = flag.Key,
            Value = variation.Value,
            VariationId = variation.Id.ToString(),
            VariationName = variation.Name,
            Kind = flag.VariationType,
            Reason = "DEFAULT",
            Version = flag.Version
        };
    }

    private static double GetBucket(string seed)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(seed));
        var n = BitConverter.ToInt32(hash, 0);
        var bucket = (n & 0x7fffffff) % 100000 / 100000.0;
        return bucket;
    }
} 