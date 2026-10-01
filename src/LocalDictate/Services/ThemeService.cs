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
            Set(resources, "BgBrush", 0xE7, 0xF1, 0xFB);
            Set(resources, "CardBrush", 0xF5, 0xFA, 0xFE);
            Set(resources, "CardAltBrush", 0xD5, 0xE7, 0xF6);
            Set(resources, "TextBrush", 0x17, 0x30, 0x44);
            Set(resources, "MutedBrush", 0x5C, 0x73, 0x86);
            Set(resources, "AccentBrush", 0x1F, 0x74, 0xE0);
            Set(resources, "AccentTealBrush", 0x14, 0x9A, 0xAB);
            Set(resources, "AccentSoftBrush", 0xD7, 0xE9, 0xF8);
            Set(resources, "TealSoftBrush", 0xD4, 0xF3, 0xF4);
            Set(resources, "LineBrush", 0xC9, 0xDA, 0xEA);
            Set(resources, "TrackBrush", 0xD8, 0xE3, 0xEE);
            Set(resources, "SwitchBrush", 0x34, 0xC7, 0x59);
            Set(resources, "ThumbBrush", 0xF7, 0xFB, 0xFE);
            Set(resources, "OverlayBrush", 0xEA, 0xF3, 0xFB, 240);
            Set(resources, "OverlayEdgeBrush", 0xF4, 0xF8, 0xFC);
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
            Set(resources, "TealSoftBrush", 0x14, 0x3C, 0x42);
            Set(resources, "LineBrush", 0x31, 0x48, 0x5E);
            Set(resources, "TrackBrush", 0x22, 0x34, 0x48);
            Set(resources, "SwitchBrush", 0x30, 0xD1, 0x58);
            Set(resources, "ThumbBrush", 0xF7, 0xFB, 0xFE);
            Set(resources, "OverlayBrush", 0x1A, 0x29, 0x40, 242);
            Set(resources, "OverlayEdgeBrush", 0x3A, 0x52, 0x68);
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

    private static void Set(ResourceDictionary resources, string key, byte r, byte g, byte b, byte a = 255)
    {
        resources[key] = new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}
