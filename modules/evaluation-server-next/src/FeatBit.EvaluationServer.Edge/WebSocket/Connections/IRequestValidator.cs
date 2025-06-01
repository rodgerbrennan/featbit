using FeatBit.EvaluationServer.Shared.Models;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Connections;

public interface IRequestValidator
{
    Task<ValidationResult> ValidateAsync(ConnectionContext context);
}

public class ValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; }
    public Secret[] Secrets { get; }

    private ValidationResult(bool isValid, string reason, Secret[] secrets)
    {
        IsValid = isValid;
        Reason = reason;
        Secrets = secrets;
    }

    public static ValidationResult Ok(Secret[] secrets) => new(true, string.Empty, secrets);
    public static ValidationResult Failed(string reason) => new(false, reason, Array.Empty<Secret>());
} 