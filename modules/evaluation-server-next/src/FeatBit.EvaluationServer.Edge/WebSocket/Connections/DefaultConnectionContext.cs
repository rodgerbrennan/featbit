using System.Net;
using FeatBit.EvaluationServer.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeatBit.EvaluationServer.Edge.WebSocket.Connections;

internal sealed class DefaultConnectionContext : ConnectionContext
{
    private readonly HttpContext _httpContext;

    public override string? RawQuery { get; }
    public override System.Net.WebSockets.WebSocket WebSocket { get; }
    public override string Type { get; }
    public override string Version { get; }
    public override string Token { get; }
    public override Client? Client { get; protected set; }
    public override Connection Connection { get; protected set; }
    public override Connection[] MappedRpConnections { get; protected set; }
    public override long ConnectAt { get; }
    public override long ClosedAt { get; protected set; }

    public DefaultConnectionContext(System.Net.WebSockets.WebSocket websocket, HttpContext httpContext)
    {
        _httpContext = httpContext;

        RawQuery = httpContext.Request.QueryString.Value;
        WebSocket = websocket;

        var query = httpContext.Request.Query;
        Type = query["type"].ToString();
        Version = query["version"].ToString();
        Token = query["token"].ToString();
        ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Client = null;
        Connection = null!;
        MappedRpConnections = [];
    }

    public async Task PrepareForProcessingAsync(Secret[] secrets)
    {
        await ResolveClientAsync();

        if (Type == ConnectionType.RelayProxy)
        {
            MappedRpConnections = secrets
                .Select(secret => new Connection(WebSocket, secret))
                .ToArray();
        }
        else
        {
            Connection = new Connection(WebSocket, secrets[0]);
        }

        return;

        async Task ResolveClientAsync()
        {
            var logger = _httpContext.RequestServices.GetRequiredService<ILogger<ConnectionContext>>();

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
    }
} 