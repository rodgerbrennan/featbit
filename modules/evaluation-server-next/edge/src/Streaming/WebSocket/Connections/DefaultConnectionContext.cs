using System.Net;
using System.Net.WebSockets;
using FeatBit.EvaluationServer.Edge.Domain.Models;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Connections;

public sealed class DefaultConnectionContext : FeatBit.EvaluationServer.Edge.Domain.Models.ConnectionContext
{
    private readonly HttpContext _httpContext;
    private readonly CancellationTokenSource _cts;

    public CancellationToken ConnectionClosed => _cts.Token;

    public override string? RawQuery { get; protected init; }
    public override System.Net.WebSockets.WebSocket WebSocket { get; }
    public override string Type { get; protected init; }
    public override string Version { get; protected init; }
    public override string Token { get; protected init; }
    public override Client? Client { get; protected set; }
    public override Connection Connection { get; protected init; }
    public override Connection[] MappedRpConnections { get; protected set; }
    public override long ConnectAt { get; protected init; }
    public override long ClosedAt { get; protected set; }

    private DefaultConnectionContext(
        System.Net.WebSockets.WebSocket websocket, 
        HttpContext httpContext,
        Connection connection) 
        : base(websocket)
    {
        _httpContext = httpContext;
        _cts = new CancellationTokenSource();

        RawQuery = httpContext.Request.QueryString.Value;
        WebSocket = websocket;

        var query = httpContext.Request.Query;
        Type = query["type"].ToString();
        Version = query["version"].ToString();
        Token = query["token"].ToString();
        ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Connection = connection;
        MappedRpConnections = Array.Empty<Connection>();
    }

    public static async Task<DefaultConnectionContext> CreateAsync(
        System.Net.WebSockets.WebSocket websocket, 
        HttpContext httpContext,
        Secret[] secrets)
    {
        var query = httpContext.Request.Query;
        var type = query["type"].ToString();
        
        Connection connection;
        Connection[] mappedConnections = [];
        
        if (type == ConnectionType.RelayProxy)
        {
            mappedConnections = secrets
                .Select(secret => new Connection(websocket, secret))
                .ToArray();
            connection = mappedConnections[0]; // Use first connection as primary
        }
        else
        {
            connection = new Connection(websocket, secrets[0]);
        }

        var context = new DefaultConnectionContext(websocket, httpContext, connection);
        
        if (mappedConnections.Length > 0)
        {
            context.MappedRpConnections = mappedConnections;
        }

        await context.ResolveClientAsync();
        return context;
    }

    private async Task ResolveClientAsync()
    {
        var logger = _httpContext.RequestServices.GetRequiredService<ILogger<DefaultConnectionContext>>();

        var ipAddr = GetIpAddr();
        var host = await GetHostAsync();

        Client = new Client(ipAddr, host);
        return;

        string GetIpAddr()
        {
            if (_httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardForHeaders))
            {
                return forwardForHeaders.FirstOrDefault(string.Empty)!;
            }

            // cloudflare connecting IP header
            // https://developers.cloudflare.com/fundamentals/reference/http-request-headers/#cf-connecting-ip
            if (_httpContext.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfConnectingIpHeaders))
            {
                return cfConnectingIpHeaders.FirstOrDefault(string.Empty)!;
            }

            var remoteIpAddr = _httpContext.Connection.RemoteIpAddress?.ToString();
            return remoteIpAddr ?? string.Empty;
        }

        async Task<string> GetHostAsync()
        {
            try
            {
                var ipAddress = IPAddress.Parse(GetIpAddr());
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve host name for IP: {IpAddress}", ipAddr);
                return string.Empty;
            }
        }
    }

    public void MarkAsDisconnected()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            ClosedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        
        if (WebSocket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            await WebSocket.CloseAsync(
                System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, 
                "Connection closed", 
                CancellationToken.None);
        }
        
        WebSocket.Dispose();
    }
} 