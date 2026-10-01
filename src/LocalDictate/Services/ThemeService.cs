using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LocalDictate.Services;

/// <summary>
/// Locked UI palette: warmer blue tint, never pure white.
/// Light is the approved main/settings surface. Dark is the same hue family
/// for an OS dark theme, still blue-tinted.
/// </summary>
public static class ThemeService
{
    public static bool UseLight { get; private set; } = true;

    public static void ApplySystemTheme()
    {
        Apply(DetectLightTheme());
    }

    public static void Apply(bool light)
    {
        UseLight = light;
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        if (light)
        {
            Set(resources, "BgBrush", 0xE3, 0xED, 0xF6);
            Set(resources, "CardBrush", 0xF3, 0xF8, 0xFC);
            Set(resources, "CardAltBrush", 0xD9, 0xE8, 0xF4);
            Set(resources, "TextBrush", 0x17, 0x30, 0x44);
            Set(resources, "MutedBrush", 0x5C, 0x73, 0x86);
            Set(resources, "AccentBrush", 0x1F, 0x74, 0xE0);
            Set(resources, "AccentTealBrush", 0x14, 0x9A, 0xAB);
            Set(resources, "AccentSoftBrush", 0xD4, 0xE7, 0xF7);
            Set(resources, "LineBrush", 0xC5, 0xD6, 0xE6);
            Set(resources, "TrackBrush", 0xD5, 0xE4, 0xF0);
        }
        else
        {
            Set(resources, "BgBrush", 0x12, 0x1C, 0x28);
            Set(resources, "CardBrush", 0x1A, 0x29, 0x40);
            Set(resources, "CardAltBrush", 0x24, 0x36, 0x4C);
            Set(resources, "TextBrush", 0xE7, 0xF2, 0xFA);
            Set(resources, "MutedBrush", 0x9B, 0xB3, 0xC7);
            Set(resources, "AccentBrush", 0x5A, 0xA6, 0xFF);
            Set(resources, "AccentTealBrush", 0x3D, 0xCF, 0xC8);
            Set(resources, "AccentSoftBrush", 0x1C, 0x3D, 0x58);
            Set(resources, "LineBrush", 0x31, 0x48, 0x5E);
            Set(resources, "TrackBrush", 0x22, 0x34, 0x48);
        }
    }

    public static bool DetectLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int mode)
            {
                return mode != 0;
            }
        }
        catch
        {
            // Fall through to the locked light palette.
        }

        return true;
    }

    private static void Set(ResourceDictionary resources, string key, byte r, byte g, byte b)
    {
        resources[key] = new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
