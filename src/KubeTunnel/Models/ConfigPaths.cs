namespace KubeTunnel.Models;

public static class ConfigPaths
{
    public static string BaseFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KubeTunnelConfig");

    public static string ConfigFile => Path.Combine(BaseFolder, "config.json");

    public static string ProfilesFolder => Path.Combine(BaseFolder, "Profiles");

    public static string ProfileFile(string profileName) =>
        Path.Combine(ProfilesFolder, $"{profileName}.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseFolder);
        Directory.CreateDirectory(ProfilesFolder);
    }
}
