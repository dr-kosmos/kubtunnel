namespace KubeTunnel.Models;

public record ServicePort(int Port, string Protocol)
{
    public override string ToString() => $"{Port}/{Protocol}";
}

public record ServiceInfo
{
    public required string Namespace { get; init; }
    public required string Service { get; init; }
    public required string PortsDisplay { get; init; }
    public required List<ServicePort> Ports { get; init; }
}
