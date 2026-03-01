using Avalonia.Styling;

namespace KubeTunnel.Models;

public record AppTheme(
    string Name,
    ThemeVariant Variant,
    string AccentColor,
    string? RegionColor = null,
    string? SurfaceColor = null,
    string? TextColor = null)
{
    private const string DefaultAccent = "#005FB8";

    public static readonly List<AppTheme> Presets =
    [
        new("Default Light", ThemeVariant.Light, DefaultAccent),
        new("Default Dark",  ThemeVariant.Dark,  DefaultAccent),
        new("Nord",           ThemeVariant.Dark, "#5E81AC", "#2E3440", "#3B4252", "#ECEFF4"),
        new("Dracula",        ThemeVariant.Dark, "#BD93F9", "#282A36", "#44475A", "#F8F8F2"),
        new("Solarized Dark", ThemeVariant.Dark, "#268BD2", "#002B36", "#073642", "#93A1A1"),
    ];

    public override string ToString() => Name;
}
