using System;
using FeatBit.EvaluationServer.Edge.Domain.Interfaces;
using FeatBit.EvaluationServer.Edge.Infrastructure;
using FeatBit.EvaluationServer.Edge.WebSocket;
using FeatBit.EvaluationServer.Edge.WebSocket.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FeatBit.EvaluationServer.Edge.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEdgeServices(
        this IServiceCollection services,
        Action<StreamingOptions>? configure = null)
    {
        // Configure options
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Add core services
        services.AddSingleton<IConnectionManager, ConnectionManager>();
        services.AddSingleton<MessageDispatcher>();

        // Add WebSocket support
        services.Configure<WebSocketOptions>(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        });

        return services;
    }
} 