using Edge.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace Edge.Api;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEdgeStreaming(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService(typeof(IOptions<StreamingOptions>)) as IOptions<StreamingOptions>;
        var streamingOptions = options?.Value ?? new StreamingOptions();

        app.UseWebSockets();
        
        app.Map(streamingOptions.PathMatch, builder =>
        {
            builder.UseMiddleware<StreamingMiddleware>();
        });

        return app;
    }
} 