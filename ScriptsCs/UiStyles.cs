#nullable enable
using Godot;

namespace GodotGdc.V1;

public static class UiStyles
{
    public sealed class Palette
    {
        public bool HackerMode { get; init; }
        public Color Accent { get; init; } = new(0.32f, 0.82f, 0.47f);
        public Color AccentSoft { get; init; } = new(0.28f, 0.62f, 0.40f);
        public Color AccentDark { get; init; } = new(0.14f, 0.28f, 0.17f);
        public Color Background { get; init; } = Colors.Black;
        public Color Surface { get; init; } = new(0.05f, 0.08f, 0.06f);
        public Color SurfaceAlt { get; init; } = new(0.08f, 0.11f, 0.09f);
        public Color SurfaceSoft { get; init; } = new(0.12f, 0.16f, 0.13f);
        public Color TextStrong { get; init; } = new(0.95f, 0.98f, 0.96f);
        public Color TextMuted { get; init; } = new(0.73f, 0.81f, 0.76f);
        public Color Warning { get; init; } = new(0.91f, 0.40f, 0.42f);
        public Color Highlight { get; init; } = new(0.82f, 0.92f, 0.78f);
    }

    public static Palette BuildPalette(Color accent, bool hackerMode = false)
    {
        if (hackerMode)
        {
            return new Palette
            {
                HackerMode = true,
                Accent = new Color(0.35f, 1.0f, 0.45f),
                AccentSoft = new Color(0.12f, 0.42f, 0.18f),
                AccentDark = new Color(0.0f, 0.05f, 0.0f),
                Background = Colors.Black,
                Surface = Colors.Black,
                SurfaceAlt = Colors.Black,
                SurfaceSoft = new Color(0.01f, 0.06f, 0.02f),
                TextStrong = new Color(0.42f, 1.0f, 0.50f),
                TextMuted = new Color(0.72f, 1.0f, 0.76f),
                Warning = Colors.White,
                Highlight = Colors.White
            };
        }

        var normalized = new Color(
            Mathf.Clamp(accent.R, 0.18f, 0.98f),
            Mathf.Clamp(accent.G, 0.18f, 0.98f),
            Mathf.Clamp(accent.B, 0.18f, 0.98f)
        );

        return new Palette
        {
            HackerMode = false,
            Accent = normalized,
            AccentSoft = normalized.Darkened(0.25f),
            AccentDark = normalized.Darkened(0.78f),
            Background = normalized.Darkened(0.93f),
            Surface = normalized.Darkened(0.88f),
            SurfaceAlt = normalized.Darkened(0.82f),
            SurfaceSoft = normalized.Darkened(0.72f),
            TextStrong = new Color(0.96f, 0.99f, 0.97f),
            TextMuted = new Color(0.75f, 0.82f, 0.77f),
            Warning = new Color(0.89f, 0.35f, 0.38f),
            Highlight = normalized.Lightened(0.32f)
        };
    }

    public static Theme GetTheme(Palette palette)
    {
        var theme = new Theme();

        var sans = new SystemFont
        {
            FontNames = new[] { "Segoe UI", "Arial", "Liberation Sans" }
        };
        var mono = new SystemFont
        {
            FontNames = new[] { "Consolas", "Cascadia Mono", "Courier New" }
        };

        var defaultFont = palette.HackerMode ? mono : sans;
        theme.DefaultFont = defaultFont;
        theme.DefaultFontSize = 20;

        theme.SetFont("font", "Label", defaultFont);
        theme.SetFontSize("font_size", "Label", 20);
        theme.SetFont("font", "Button", defaultFont);
        theme.SetFontSize("font_size", "Button", palette.HackerMode ? 24 : 20);
        theme.SetFont("font", "LineEdit", defaultFont);
        theme.SetFontSize("font_size", "LineEdit", 20);
        theme.SetFont("font", "RichTextLabel", defaultFont);
        theme.SetFontSize("normal_font_size", "RichTextLabel", 19);
        theme.SetFont("font", "ItemList", defaultFont);
        theme.SetFontSize("font_size", "ItemList", 20);

        theme.SetColor("font_color", "Label", palette.TextStrong);
        theme.SetColor("font_color", "Button", palette.TextStrong);
        theme.SetColor("font_color", "LineEdit", palette.TextStrong);
        theme.SetColor("default_color", "RichTextLabel", palette.TextStrong);

        var buttonNormal = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.SurfaceAlt, 8, palette.AccentDark.Lightened(0.28f));
        var buttonHover = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.SurfaceSoft, 8, palette.Accent);
        var buttonPressed = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.AccentDark, 8, palette.AccentSoft);
        var buttonDisabled = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.Background.Lightened(0.06f), 8, palette.Surface);
        theme.SetStylebox("normal", "Button", buttonNormal);
        theme.SetStylebox("hover", "Button", buttonHover);
        theme.SetStylebox("pressed", "Button", buttonPressed);
        theme.SetStylebox("disabled", "Button", buttonDisabled);

        var panel = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.Surface, 12, palette.AccentDark.Lightened(0.22f));
        var panelAlt = palette.HackerMode
            ? MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0)
            : MakeFlatBox(palette.SurfaceAlt, 12, palette.AccentDark.Lightened(0.30f));
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "PopupPanel", panelAlt);

        theme.SetStylebox("normal", "LineEdit", MakeFlatBox(palette.SurfaceAlt, 8, palette.AccentDark.Lightened(0.35f)));
        theme.SetStylebox("focus", "LineEdit", MakeFlatBox(palette.SurfaceAlt, 8, palette.Accent));

        theme.SetStylebox("panel", "ScrollContainer", MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0)));
        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 10);
        theme.SetConstant("line_separation", "RichTextLabel", 3);
        theme.SetConstant("v_separation", "ItemList", 6);

        theme.SetColor("font_selected_color", "ItemList", palette.TextStrong);
        theme.SetColor("font_hovered_color", "ItemList", palette.TextStrong);
        theme.SetColor("guide_color", "ItemList", palette.TextMuted);
        theme.SetColor("font_color", "ItemList", palette.TextMuted);
        theme.SetColor("cursor_color", "LineEdit", palette.Accent);
        theme.SetColor("selection_color", "LineEdit", palette.Accent * new Color(1, 1, 1, 0.35f));

        return theme;
    }

    public static StyleBoxFlat MakeFlatBox(Color bg, int cornerRadius = 10, Color? border = null, int borderWidth = 1)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderColor = border ?? bg.Lightened(0.08f),
            ContentMarginBottom = 10,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 10
        };
        return box;
    }

    public static Label MakeHeading(string text, int fontSize = 28, Palette? palette = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", palette?.TextStrong ?? Colors.White);
        return label;
    }

    public static Label MakeSubtle(string text, int fontSize = 14, Palette? palette = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", palette?.TextMuted ?? new Color(0.7f, 0.7f, 0.7f));
        return label;
    }

    public static Label MakeMonoValue(string text, int fontSize = 28, Palette? palette = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontOverride("font", GetMonoFont());
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", palette?.Highlight ?? Colors.White);
        return label;
    }

    public static Label MakeCenteredLabel(string text, int fontSize = 18, Palette? palette = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", palette?.TextStrong ?? Colors.White);
        return label;
    }

    public static PanelContainer MakeSurface(string title, Palette palette, bool alt = false)
    {
        var panel = new PanelContainer();
        if (palette.HackerMode)
        {
            panel.AddThemeStyleboxOverride("panel", MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0));
        }
        else
        {
            panel.AddThemeStyleboxOverride("panel", alt ? MakeFlatBox(palette.SurfaceAlt, 12, palette.AccentDark.Lightened(0.26f)) : MakeFlatBox(palette.Surface, 12, palette.AccentDark.Lightened(0.20f)));
        }
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        panel.AddChild(body);
        if (!string.IsNullOrEmpty(title))
        {
            body.AddChild(MakeHeading(title, 24, palette));
        }
        return panel;
    }

    public static Button MakeAccentButton(string text, Palette palette, Color? accentOverride = null)
    {
        var accent = accentOverride ?? palette.Accent;
        var button = new Button { Text = palette.HackerMode ? $"> {text}" : text };
        if (palette.HackerMode)
        {
            button.AddThemeStyleboxOverride("normal", MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0));
            button.AddThemeStyleboxOverride("hover", MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0));
            button.AddThemeStyleboxOverride("pressed", MakeFlatBox(new Color(0, 0, 0, 0), 0, new Color(0, 0, 0, 0), 0));
            button.AddThemeColorOverride("font_color", accent);
            button.AddThemeFontOverride("font", GetMonoFont());
            button.AddThemeFontSizeOverride("font_size", 28);
        }
        else
        {
            button.AddThemeStyleboxOverride("normal", MakeFlatBox(accent.Darkened(0.28f), 10, accent.Lightened(0.10f), 2));
            button.AddThemeStyleboxOverride("hover", MakeFlatBox(accent.Darkened(0.10f), 10, accent.Lightened(0.22f), 2));
            button.AddThemeStyleboxOverride("pressed", MakeFlatBox(accent.Darkened(0.40f), 10, accent.Darkened(0.15f), 2));
        }
        button.AddThemeColorOverride("font_color", palette.TextStrong);
        return button;
    }

    public static ColorRect MakeBackground(Palette palette)
    {
        return new ColorRect { Color = palette.Background };
    }

    public static ColorPickerButton MakeThemePicker(Palette palette, Color initial)
    {
        var picker = new ColorPickerButton
        {
            Color = initial,
            Text = "Theme Color"
        };
        picker.AddThemeStyleboxOverride("normal", MakeFlatBox(palette.SurfaceSoft, 10, palette.Accent, 2));
        picker.AddThemeStyleboxOverride("hover", MakeFlatBox(palette.SurfaceAlt, 10, palette.Accent.Lightened(0.15f), 2));
        return picker;
    }

    public static Font GetMonoFont()
    {
        return new SystemFont
        {
            FontNames = new[] { "Consolas", "Cascadia Mono", "Courier New" }
        };
    }

    public static void ApplyWindowScale(Window window, float uiScale)
    {
        window.ContentScaleFactor = Mathf.Clamp(uiScale, 0.8f, 1.5f);
    }
}
