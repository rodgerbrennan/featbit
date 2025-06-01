using Microsoft.AspNetCore.Builder;

namespace FeatBit.EvaluationServer.Edge.WebSocket;

public static class StreamingMiddlewareExtension
{
    public static IApplicationBuilder UseStreaming(this IApplicationBuilder builder)
    {
        return builder
            .UseWebSockets()
            .UseMiddleware<StreamingMiddleware>();
    }
} 