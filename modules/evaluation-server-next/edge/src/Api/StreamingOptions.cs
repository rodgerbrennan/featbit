namespace Edge.Api;

public class StreamingOptions
{
    public string PathMatch { get; set; } = "/streaming";
    public int KeepAliveInterval { get; set; } = 30; // seconds
    public int ReceiveBufferSize { get; set; } = 4 * 1024; // 4KB
    public int MaxMessageSize { get; set; } = 32 * 1024; // 32KB
} 