using Avalonia.Media;

namespace RemoteDesktopClient.UI.Avalonia;

/// <summary>A font description bundling the pieces Avalonia keeps as separate
/// properties on TextBlock/TextBox (there's no single Font object like WinForms').</summary>
internal sealed record AppFont(string Family, double Size, FontWeight Weight = FontWeight.Normal, FontStyle Style = FontStyle.Normal);

/// <summary>Centralized colors and fonts so every custom control stays visually consistent.
/// Named AppTheme (not Theme) because Avalonia's own StyledElement base class already
/// declares an instance property called Theme (its ControlTheme styling mechanism) —
/// "Theme" would be silently shadowed inside every Control-derived class.</summary>
internal static class AppTheme
{
    public static readonly Color Background = Color.FromRgb(0xEF, 0xF2, 0xFD);
    public static readonly Color CardBackground = Colors.White;
    public static readonly Color CardBorder = Color.FromRgb(0xE4, 0xE8, 0xF3);

    public static readonly Color TextPrimary = Color.FromRgb(0x1A, 0x1D, 0x29);
    public static readonly Color TextSecondary = Color.FromRgb(0x6B, 0x72, 0x80);

    public static readonly Color Accent = Color.FromRgb(0x2F, 0x5F, 0xED);
    public static readonly Color AccentHover = Color.FromRgb(0x27, 0x51, 0xD6);
    public static readonly Color AccentPressed = Color.FromRgb(0x1F, 0x44, 0xB8);

    public static readonly Color HeroBackground = Color.FromRgb(0xE3, 0xEA, 0xFC);

    public static readonly Color SubtleButtonBackground = Color.FromRgb(0xF2, 0xF3, 0xF7);
    public static readonly Color SubtleButtonHover = Color.FromRgb(0xE7, 0xE9, 0xF0);

    public static readonly Color Online = Color.FromRgb(0x22, 0xC5, 0x5E);
    public static readonly Color Offline = Color.FromRgb(0x9A, 0xA3, 0xAF);
    public static readonly Color OnlineSoft = Color.FromRgb(0xE1, 0xFA, 0xEB);
    public static readonly Color OfflineSoft = Color.FromRgb(0xEE, 0xF0, 0xF3);

    public static readonly Color AccentSoft = Color.FromRgb(0xE3, 0xEA, 0xFC);
    public static readonly Color AccentSoftHover = Color.FromRgb(0xD3, 0xDF, 0xFA);
    public static readonly Color AccentSoftText = Color.FromRgb(0x2F, 0x5F, 0xED);

    // Per-device-type icon badge tints, used in the Recent Connections grid.
    public static readonly Color IconBlueBg = Color.FromRgb(0xDC, 0xE8, 0xFD);
    public static readonly Color IconBlueFg = Color.FromRgb(0x2F, 0x5F, 0xED);
    public static readonly Color IconPurpleBg = Color.FromRgb(0xF0, 0xE6, 0xFB);
    public static readonly Color IconPurpleFg = Color.FromRgb(0x9B, 0x5C, 0xD9);
    public static readonly Color IconGreenBg = Color.FromRgb(0xDF, 0xF6, 0xE8);
    public static readonly Color IconGreenFg = Color.FromRgb(0x22, 0xA3, 0x5A);
    public static readonly Color IconOrangeBg = Color.FromRgb(0xFD, 0xEB, 0xD9);
    public static readonly Color IconOrangeFg = Color.FromRgb(0xE0, 0x7A, 0x2E);

    public static readonly AppFont FontTitle = new("Segoe UI Semibold", 21);
    public static readonly AppFont FontSubtitle = new("Segoe UI", 12);
    public static readonly AppFont FontSectionHeader = new("Segoe UI Semibold", 16);
    public static readonly AppFont FontBody = new("Segoe UI", 12.5);
    public static readonly AppFont FontBodyBold = new("Segoe UI Semibold", 12.5);
    public static readonly AppFont FontSmall = new("Segoe UI", 11.5);
    public static readonly AppFont FontMonoId = new("Consolas", 18, FontWeight.Bold);
}
