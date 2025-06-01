namespace FeatBit.EvaluationServer.Hub.Domain.Evaluation;

public interface IFlagEvaluator
{
    EvaluationResult Evaluate(Flag flag, Target target);
} 