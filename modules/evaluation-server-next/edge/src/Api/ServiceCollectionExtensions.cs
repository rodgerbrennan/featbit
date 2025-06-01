using System;
using Edge.Domain.Interfaces;
using Edge.Infrastructure;
using Edge.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace Edge.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeServices(
        this IServiceCollection services,
        Action<StreamingOptions> configure = null)
    {
        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }

        // Add core services
        services.AddSingleton<IConnectionManager, ConnectionManager>();
        services.AddSingleton<MessageDispatcher>();

        // Add WebSocket support
        services.AddWebSockets(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            options.ReceiveBufferSize = 4 * 1024;
        });

        return services;
    }
} 