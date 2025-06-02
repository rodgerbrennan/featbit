namespace FeatBit.EvaluationServer.Hub.Domain.Common.Models;

public static class ConnectionType
{
    public const string Client = "client";
    public const string Server = "server";

    public static readonly string[] All = { Client, Server };
} 