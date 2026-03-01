namespace KubeTunnel.Models;

public record PortForwardConfig
{
    public required string Namespace { get; init; }
    public required string Service { get; init; }
    public required int LocalPort { get; init; }
    public required int RemotePort { get; init; }
    public string Protocol { get; init; } = "TCP";
}
