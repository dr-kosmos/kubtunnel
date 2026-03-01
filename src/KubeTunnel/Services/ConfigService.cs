using System.Diagnostics;
using System.Text.Json;
using KubeTunnel.Models;

namespace KubeTunnel.Services;

public class ConfigService
{
    public Config LoadConfig()
    {
        ConfigPaths.EnsureDirectories();

        if (!File.Exists(ConfigPaths.ConfigFile))
        {
            var newConfig = new Config();
            SaveConfig(newConfig);
            return newConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigPaths.ConfigFile);
            return JsonSerializer.Deserialize<Config>(json) ?? new Config();
        }
        catch
        {
            return new Config();
        }
    }

    public void SaveConfig(Config config)
    {
        ConfigPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(ConfigPaths.ConfigFile, json);
    }

    public List<string> GetProfileList()
    {
        ConfigPaths.EnsureDirectories();

        var files = Directory.GetFiles(ConfigPaths.ProfilesFolder, "*.json");
        if (files.Length == 0)
            return [Config.DefaultConfigName];

        return files.Select(Path.GetFileNameWithoutExtension).Where(n => n != null).Cast<string>().ToList();
    }

    public PortForwardConfig[] LoadProfile(string profileName)
    {
        try
        {
            var path = ConfigPaths.ProfileFile(profileName);
            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PortForwardConfig[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void DeleteProfile(string profileName)
    {
        var path = ConfigPaths.ProfileFile(profileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void SaveProfile(string profileName, IEnumerable<PortForwardConfig> configs)
    {
        ConfigPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(configs.ToArray());
        File.WriteAllText(ConfigPaths.ProfileFile(profileName), json);
    }

    public async Task<List<ServiceInfo>> LoadServicesAsync()
    {
        var services = new List<ServiceInfo>();

        var startInfo = new ProcessStartInfo
        {
            FileName = "kubectl",
            Arguments = "get services --all-namespaces",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch
        {
            return services;
        }

        if (process == null)
            return services;

        var firstLine = true;
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) != null)
        {
            if (firstLine)
            {
                firstLine = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var split = line.Split(' ', 7,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var portsRaw = split[5]; // e.g. "80/TCP,443/TCP,9090/UDP"
                var ports = portsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry =>
                    {
                        var parts = entry.Split('/');
                        return new ServicePort(int.Parse(parts[0]), parts.Length > 1 ? parts[1] : "TCP");
                    })
                    .ToList();

                services.Add(new ServiceInfo
                {
                    Service = split[1],
                    Namespace = split[0],
                    PortsDisplay = portsRaw,
                    Ports = ports
                });
            }
            catch
            {
                // ignored — unparseable line
            }
        }

        return services;
    }
}
