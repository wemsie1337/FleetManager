using System.Drawing;

namespace FleetManager.UI.Theme;

/// <summary>Палитра приложения (светлая/тёмная).</summary>
public sealed class ThemePalette
{
    public required string Name { get; init; }
    public required bool IsDark { get; init; }

    public required Color Background { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceAlt { get; init; }
    public required Color SurfaceHover { get; init; }
    public required Color Border { get; init; }
    public required Color Text { get; init; }
    public required Color TextMuted { get; init; }

    public required Color Primary { get; init; }
    public required Color PrimaryLight { get; init; }
    public required Color PrimaryDark { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentDark { get; init; }
    public required Color Teal { get; init; }

    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Danger { get; init; }
    public required Color Info { get; init; }

    public required Color SidebarStart { get; init; }
    public required Color SidebarEnd { get; init; }
    public required Color TopBar { get; init; }
    public required Color GridHeader { get; init; }
    public required Color GridAltRow { get; init; }
    public required Color GridSelection { get; init; }
    public required Color Shadow { get; init; }

    public static ThemePalette Light { get; } = new()
    {
        Name = "Светлая",
        IsDark = false,
        Background = Color.FromArgb(244, 247, 251),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(241, 245, 249),
        SurfaceHover = Color.FromArgb(226, 232, 240),
        Border = Color.FromArgb(216, 226, 240),
        Text = Color.FromArgb(15, 23, 42),
        TextMuted = Color.FromArgb(100, 116, 139),
        Primary = Color.FromArgb(30, 58, 138),
        PrimaryLight = Color.FromArgb(37, 99, 235),
        PrimaryDark = Color.FromArgb(23, 45, 110),
        Accent = Color.FromArgb(249, 115, 22),
        AccentDark = Color.FromArgb(234, 88, 12),
        Teal = Color.FromArgb(20, 184, 166),
        Success = Color.FromArgb(22, 163, 74),
        Warning = Color.FromArgb(245, 158, 11),
        Danger = Color.FromArgb(220, 38, 38),
        Info = Color.FromArgb(14, 165, 233),
        SidebarStart = Color.FromArgb(11, 27, 58),
        SidebarEnd = Color.FromArgb(30, 58, 138),
        TopBar = Color.White,
        GridHeader = Color.FromArgb(238, 244, 255),
        GridAltRow = Color.FromArgb(248, 250, 252),
        GridSelection = Color.FromArgb(219, 234, 254),
        Shadow = Color.FromArgb(16, 15, 23, 42)
    };

    public static ThemePalette Dark { get; } = new()
    {
        Name = "Тёмная",
        IsDark = true,
        Background = Color.FromArgb(7, 13, 24),
        Surface = Color.FromArgb(15, 26, 43),
        SurfaceAlt = Color.FromArgb(22, 36, 58),
        SurfaceHover = Color.FromArgb(30, 48, 80),
        Border = Color.FromArgb(35, 55, 85),
        Text = Color.FromArgb(232, 238, 249),
        TextMuted = Color.FromArgb(143, 163, 192),
        Primary = Color.FromArgb(37, 99, 235),
        PrimaryLight = Color.FromArgb(59, 130, 246),
        PrimaryDark = Color.FromArgb(29, 78, 216),
        Accent = Color.FromArgb(251, 146, 60),
        AccentDark = Color.FromArgb(249, 115, 22),
        Teal = Color.FromArgb(45, 212, 191),
        Success = Color.FromArgb(34, 197, 94),
        Warning = Color.FromArgb(250, 204, 21),
        Danger = Color.FromArgb(248, 113, 113),
        Info = Color.FromArgb(56, 189, 248),
        SidebarStart = Color.FromArgb(4, 10, 20),
        SidebarEnd = Color.FromArgb(14, 30, 58),
        TopBar = Color.FromArgb(15, 26, 43),
        GridHeader = Color.FromArgb(22, 36, 58),
        GridAltRow = Color.FromArgb(12, 22, 38),
        GridSelection = Color.FromArgb(30, 58, 95),
        Shadow = Color.FromArgb(60, 0, 0, 0)
    };
}
