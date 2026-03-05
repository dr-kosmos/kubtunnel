namespace KubeTunnel.Models;

public class Config
{
    public const string DefaultConfigName = "default";
    public string CurrentProfile { get; set; } = DefaultConfigName;
    public string Theme { get; set; } = "Default Dark";
    public bool DnsMode { get; set; }
}