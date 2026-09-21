namespace RemoteDesktopClient.UI.WinForms;

/// <summary>Centralized colors and fonts so every custom control stays visually consistent.</summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(0xEF, 0xF2, 0xFD);
    public static readonly Color CardBackground = Color.White;
    public static readonly Color CardBorder = Color.FromArgb(0xE4, 0xE8, 0xF3);

    public static readonly Color TextPrimary = Color.FromArgb(0x1A, 0x1D, 0x29);
    public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x72, 0x80);

    public static readonly Color Accent = Color.FromArgb(0x2F, 0x5F, 0xED);
    public static readonly Color AccentHover = Color.FromArgb(0x27, 0x51, 0xD6);
    public static readonly Color AccentPressed = Color.FromArgb(0x1F, 0x44, 0xB8);

    public static readonly Color HeroBackground = Color.FromArgb(0xE3, 0xEA, 0xFC);

    public static readonly Color SubtleButtonBackground = Color.FromArgb(0xF2, 0xF3, 0xF7);
    public static readonly Color SubtleButtonHover = Color.FromArgb(0xE7, 0xE9, 0xF0);

    public static readonly Color Online = Color.FromArgb(0x22, 0xC5, 0x5E);
    public static readonly Color Offline = Color.FromArgb(0x9A, 0xA3, 0xAF);
    public static readonly Color OnlineSoft = Color.FromArgb(0xE1, 0xFA, 0xEB);
    public static readonly Color OfflineSoft = Color.FromArgb(0xEE, 0xF0, 0xF3);

    public static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xEA, 0xFC);
    public static readonly Color AccentSoftHover = Color.FromArgb(0xD3, 0xDF, 0xFA);
    public static readonly Color AccentSoftText = Color.FromArgb(0x2F, 0x5F, 0xED);

    // Per-device-type icon badge tints, used in the Recent Connections grid.
    public static readonly Color IconBlueBg = Color.FromArgb(0xDC, 0xE8, 0xFD);
    public static readonly Color IconBlueFg = Color.FromArgb(0x2F, 0x5F, 0xED);
    public static readonly Color IconPurpleBg = Color.FromArgb(0xF0, 0xE6, 0xFB);
    public static readonly Color IconPurpleFg = Color.FromArgb(0x9B, 0x5C, 0xD9);
    public static readonly Color IconGreenBg = Color.FromArgb(0xDF, 0xF6, 0xE8);
    public static readonly Color IconGreenFg = Color.FromArgb(0x22, 0xA3, 0x5A);
    public static readonly Color IconOrangeBg = Color.FromArgb(0xFD, 0xEB, 0xD9);
    public static readonly Color IconOrangeFg = Color.FromArgb(0xE0, 0x7A, 0x2E);

    public static readonly Font FontTitle = new("Segoe UI Semibold", 16f);
    public static readonly Font FontSubtitle = new("Segoe UI", 9f);
    public static readonly Font FontSectionHeader = new("Segoe UI Semibold", 12f);
    public static readonly Font FontBody = new("Segoe UI", 9.5f);
    public static readonly Font FontBodyBold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontMonoId = new("Consolas", 14f, FontStyle.Bold);
}
