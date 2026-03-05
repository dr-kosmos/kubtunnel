using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using KubeTunnel.Models;
using KubeTunnel.Services;
using KubeTunnel.Views;

namespace KubeTunnel;

public partial class App : Application
{
    private static readonly IStyle OverrideStyle = new Style();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            DnsRelayService.RemoveHostsEntries();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.ShutdownRequested += (_, _) =>
            {
                CleanupDnsMode();
                KillAllKubectl();
            };
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            CleanupDnsMode();
            KillAllKubectl();
        };

        base.OnFrameworkInitializationCompleted();
    }

    private static void CleanupDnsMode()
    {
        try { DnsRelayService.RemoveHostsEntries(); } catch { /* ignored */ }
    }

    private static void KillAllKubectl()
    {
        foreach (var p in Process.GetProcessesByName("kubectl"))
        {
            try { p.Kill(); }
            catch { /* ignored */ }
        }
    }

    public static void ApplyTheme(AppTheme theme)
    {
        if (Current is null) return;

        Current.RequestedThemeVariant = theme.Variant;

        // Accent via palette (uses DynamicResource internally, so this works)
        if (Current.Styles[0] is FluentTheme fluentTheme)
        {
            fluentTheme.Palettes.Remove(ThemeVariant.Dark);
            fluentTheme.Palettes.Remove(ThemeVariant.Light);

            var accent = Color.Parse(theme.AccentColor);
            fluentTheme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = accent };
            fluentTheme.Palettes[ThemeVariant.Light] = new ColorPaletteResources { Accent = accent };
        }

        // Remove previous overrides
        Current.Styles.Remove(OverrideStyle);

        if (theme.RegionColor is not null)
        {
            var bg = Color.Parse(theme.RegionColor);
            var surface = Color.Parse(theme.SurfaceColor!);
            var text = Color.Parse(theme.TextColor!);

            // FluentTheme brushes use StaticResource for colors, so changing
            // colors at runtime does nothing. We must override the Brush
            // resources themselves with new SolidColorBrush instances.
            var brushes = BuildBrushDictionary(bg, surface, text);

            ((Style)OverrideStyle).Resources.ThemeDictionaries.Clear();
            ((Style)OverrideStyle).Resources.ThemeDictionaries[ThemeVariant.Dark] = brushes;
            ((Style)OverrideStyle).Resources.ThemeDictionaries[ThemeVariant.Light] = Clone(brushes);

            Current.Styles.Add(OverrideStyle);
        }
    }

    private static ResourceDictionary BuildBrushDictionary(Color bg, Color surface, Color text)
    {
        var surfaceHigh = Lighten(surface, 0.12);
        var surfaceMed = Lighten(surface, 0.03);
        var chromeGray = Lighten(surface, 0.20);
        var disabledHi = Lighten(surface, 0.15);
        var disabledLo = Darken(surface, 0.10);
        var blackLo = Lighten(bg, 0.15);
        var blackMedLo = Lighten(bg, 0.10);
        var blackMed = Lighten(bg, 0.05);

        var d = new ResourceDictionary();

        // Region (window background)
        d["SystemRegionBrush"] = Brush(bg);

        // Background brushes
        d["SystemControlBackgroundAltHighBrush"] = Brush(bg);
        d["SystemControlBackgroundAltMediumHighBrush"] = Brush(Fade(bg, 0.80));
        d["SystemControlBackgroundAltMediumBrush"] = Brush(Fade(bg, 0.40));
        d["SystemControlBackgroundAltMediumLowBrush"] = Brush(Fade(bg, 0.18));
        d["SystemControlBackgroundBaseHighBrush"] = Brush(text);
        d["SystemControlBackgroundBaseLowBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlBackgroundBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlBackgroundBaseMediumHighBrush"] = Brush(Fade(text, 0.82));
        d["SystemControlBackgroundBaseMediumLowBrush"] = Brush(Fade(text, 0.44));
        d["SystemControlBackgroundChromeBlackHighBrush"] = Brush(Color.Parse("#FF000000"));
        d["SystemControlBackgroundChromeBlackMediumBrush"] = Brush(blackMed);
        d["SystemControlBackgroundChromeBlackLowBrush"] = Brush(blackLo);
        d["SystemControlBackgroundChromeBlackMediumLowBrush"] = Brush(blackMedLo);
        d["SystemControlBackgroundChromeMediumBrush"] = Brush(surfaceMed);
        d["SystemControlBackgroundChromeMediumLowBrush"] = Brush(surface);
        d["SystemControlBackgroundChromeWhiteBrush"] = Brush(text);
        d["SystemControlBackgroundListLowBrush"] = Brush(Lighten(surface, 0.02));
        d["SystemControlBackgroundListMediumBrush"] = Brush(Lighten(surface, 0.08));

        // Foreground brushes
        d["SystemControlForegroundBaseHighBrush"] = Brush(text);
        d["SystemControlForegroundBaseLowBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlForegroundBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlForegroundBaseMediumHighBrush"] = Brush(Fade(text, 0.82));
        d["SystemControlForegroundBaseMediumLowBrush"] = Brush(Fade(text, 0.44));
        d["SystemControlForegroundAltHighBrush"] = Brush(bg);
        d["SystemControlForegroundAltMediumHighBrush"] = Brush(Fade(bg, 0.80));
        d["SystemControlForegroundChromeBlackHighBrush"] = Brush(Color.Parse("#FF000000"));
        d["SystemControlForegroundChromeHighBrush"] = Brush(surfaceHigh);
        d["SystemControlForegroundChromeMediumBrush"] = Brush(surfaceMed);
        d["SystemControlForegroundChromeDisabledLowBrush"] = Brush(disabledLo);
        d["SystemControlForegroundChromeWhiteBrush"] = Brush(text);
        d["SystemControlForegroundChromeBlackMediumBrush"] = Brush(blackMed);
        d["SystemControlForegroundChromeBlackMediumLowBrush"] = Brush(blackMedLo);
        d["SystemControlForegroundChromeGrayBrush"] = Brush(chromeGray);
        d["SystemControlForegroundListLowBrush"] = Brush(Lighten(surface, 0.02));
        d["SystemControlForegroundListMediumBrush"] = Brush(Lighten(surface, 0.08));

        // Page background brushes
        d["SystemControlPageBackgroundAltMediumBrush"] = Brush(Fade(bg, 0.40));
        d["SystemControlPageBackgroundAltHighBrush"] = Brush(bg);
        d["SystemControlPageBackgroundMediumAltMediumBrush"] = Brush(Fade(bg, 0.40));
        d["SystemControlPageBackgroundBaseLowBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlPageBackgroundBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlPageBackgroundListLowBrush"] = Brush(Lighten(surface, 0.02));
        d["SystemControlPageBackgroundChromeLowBrush"] = Brush(surface);
        d["SystemControlPageBackgroundChromeMediumLowBrush"] = Brush(surface);

        // Page text brushes
        d["SystemControlPageTextBaseHighBrush"] = Brush(text);
        d["SystemControlPageTextBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlPageTextChromeBlackMediumLowBrush"] = Brush(blackMedLo);

        // Highlight brushes (non-accent)
        d["SystemControlHighlightBaseHighBrush"] = Brush(text);
        d["SystemControlHighlightBaseLowBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlHighlightBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlHighlightBaseMediumHighBrush"] = Brush(Fade(text, 0.82));
        d["SystemControlHighlightBaseMediumLowBrush"] = Brush(Fade(text, 0.44));
        d["SystemControlHighlightAltBaseHighBrush"] = Brush(text);
        d["SystemControlHighlightAltBaseLowBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlHighlightAltBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlHighlightAltBaseMediumHighBrush"] = Brush(Fade(text, 0.82));
        d["SystemControlHighlightAltBaseMediumLowBrush"] = Brush(Fade(text, 0.44));
        d["SystemControlHighlightAltAltHighBrush"] = Brush(bg);
        d["SystemControlHighlightAltAltMediumHighBrush"] = Brush(Fade(bg, 0.80));
        d["SystemControlHighlightAltChromeWhiteBrush"] = Brush(text);
        d["SystemControlHighlightChromeAltLowBrush"] = Brush(Lighten(surface, 0.05));
        d["SystemControlHighlightChromeHighBrush"] = Brush(surfaceHigh);
        d["SystemControlHighlightChromeWhiteBrush"] = Brush(text);
        d["SystemControlHighlightListMediumBrush"] = Brush(Lighten(surface, 0.08));
        d["SystemControlHighlightListLowBrush"] = Brush(Lighten(surface, 0.02));

        // Disabled brushes
        d["SystemControlDisabledBaseHighBrush"] = Brush(Fade(text, 0.20));
        d["SystemControlDisabledBaseLowBrush"] = Brush(Fade(text, 0.10));
        d["SystemControlDisabledBaseMediumLowBrush"] = Brush(Fade(text, 0.15));
        d["SystemControlDisabledChromeDisabledHighBrush"] = Brush(disabledHi);
        d["SystemControlDisabledChromeDisabledLowBrush"] = Brush(disabledLo);
        d["SystemControlDisabledChromeHighBrush"] = Brush(surfaceHigh);
        d["SystemControlDisabledChromeMediumLowBrush"] = Brush(surface);

        // Transient
        d["SystemControlTransientBackgroundBrush"] = Brush(surface);
        d["SystemControlTransientBorderBrush"] = Brush(Fade(text, 0.36));

        // Hyperlink
        d["SystemControlHyperlinkBaseHighBrush"] = Brush(text);
        d["SystemControlHyperlinkBaseMediumBrush"] = Brush(Fade(text, 0.60));
        d["SystemControlHyperlinkBaseMediumHighBrush"] = Brush(Fade(text, 0.82));

        // Focus
        d["SystemControlFocusVisualPrimaryBrush"] = Brush(text);
        d["SystemControlFocusVisualSecondaryBrush"] = Brush(Fade(bg, 0.40));

        // Reveal list
        d["SystemControlHighlightListLowRevealBackgroundBrush"] = Brush(Lighten(surface, 0.08));
        d["SystemControlHighlightListMediumRevealBackgroundBrush"] = Brush(Lighten(surface, 0.08));

        // Description text
        d["SystemControlDescriptionTextForegroundBrush"] = Brush(Fade(text, 0.60));

        // Menu / Context menu
        d["MenuFlyoutPresenterBackground"] = Brush(surface);
        d["MenuFlyoutPresenterBorderBrush"] = Brush(Fade(text, 0.15));
        d["MenuFlyoutItemBackground"] = Brush(Colors.Transparent);
        d["MenuFlyoutItemBackgroundPointerOver"] = Brush(surfaceHigh);
        d["MenuFlyoutItemBackgroundPressed"] = Brush(surfaceMed);
        d["MenuFlyoutItemBackgroundDisabled"] = Brush(Colors.Transparent);
        d["MenuFlyoutItemForeground"] = Brush(text);
        d["MenuFlyoutItemForegroundPointerOver"] = Brush(text);
        d["MenuFlyoutItemForegroundPressed"] = Brush(text);
        d["MenuFlyoutItemForegroundDisabled"] = Brush(Fade(text, 0.36));
        d["MenuBarItemBackground"] = Brush(Colors.Transparent);
        d["MenuBarItemBackgroundPointerOver"] = Brush(surfaceHigh);
        d["MenuBarItemBackgroundPressed"] = Brush(surfaceMed);
        d["MenuBarItemBackgroundSelected"] = Brush(surfaceMed);
        d["MenuBarBackground"] = Brush(bg);
        d["ContextMenuBackground"] = Brush(surface);
        d["ContextMenuBorderBrush"] = Brush(Fade(text, 0.15));
        d["MenuFlyoutSeparatorBackground"] = Brush(Fade(text, 0.15));

        return d;
    }

    private static SolidColorBrush Brush(Color c) => new(c);

    private static ResourceDictionary Clone(ResourceDictionary source)
    {
        var d = new ResourceDictionary();
        foreach (var kvp in source)
            d[kvp.Key] = kvp.Value is SolidColorBrush b ? new SolidColorBrush(b.Color) : kvp.Value;
        return d;
    }

    private static Color Lighten(Color c, double amount)
    {
        var r = (byte)Math.Min(255, c.R + 255 * amount);
        var g = (byte)Math.Min(255, c.G + 255 * amount);
        var b = (byte)Math.Min(255, c.B + 255 * amount);
        return Color.FromRgb(r, g, b);
    }

    private static Color Darken(Color c, double amount)
    {
        var r = (byte)Math.Max(0, c.R - 255 * amount);
        var g = (byte)Math.Max(0, c.G - 255 * amount);
        var b = (byte)Math.Max(0, c.B - 255 * amount);
        return Color.FromRgb(r, g, b);
    }

    private static Color Fade(Color c, double opacity)
    {
        return Color.FromArgb((byte)(255 * opacity), c.R, c.G, c.B);
    }
}
