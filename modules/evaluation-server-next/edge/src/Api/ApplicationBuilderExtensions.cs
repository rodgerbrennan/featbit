using FeatBit.EvaluationServer.Edge.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FeatBit.EvaluationServer.Edge.Api;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEdgeStreaming(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<IOptions<StreamingOptions>>();
        var streamingOptions = options?.Value ?? new StreamingOptions();

        app.UseWebSockets();
        
        app.Map(streamingOptions.PathMatch ?? "/streaming", builder =>
        {
            builder.UseMiddleware<StreamingMiddleware>();
        });

        return app;
    }
} 