using System;

namespace Edge.Domain.Models;

public class Connection
{
    public string Id { get; }
    public string ClientId { get; }
    public string SdkKey { get; }
    public string SdkType { get; }
    public string SdkVersion { get; }
    public DateTimeOffset ConnectedAt { get; }
    public DateTimeOffset LastPingAt { get; private set; }

    public Connection(string id, string clientId, string sdkKey, string sdkType, string sdkVersion)
    {
        Id = id;
        ClientId = clientId;
        SdkKey = sdkKey;
        SdkType = sdkType;
        SdkVersion = sdkVersion;
        ConnectedAt = DateTimeOffset.UtcNow;
        LastPingAt = ConnectedAt;
    }

    public void UpdateLastPing()
    {
        LastPingAt = DateTimeOffset.UtcNow;
    }
} 