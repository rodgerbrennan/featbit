using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Connections;

public interface IRequestValidator
{
    Task<ValidationResult> ValidateAsync(HttpContext context);
}

public class ValidationResult
{
    public bool IsValid { get; }
    public int StatusCode { get; }
    public string Message { get; }

    private ValidationResult(bool isValid, int statusCode = 400, string message = "")
    {
        IsValid = isValid;
        StatusCode = statusCode;
        Message = message;
    }

    public static ValidationResult Ok()
    {
        return new ValidationResult(true);
    }

    public static ValidationResult Failed(int statusCode = 400, string message = "")
    {
        return new ValidationResult(false, statusCode, message);
    }
} 