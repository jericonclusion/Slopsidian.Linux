using Avalonia.Media;
using Avalonia.Styling;

namespace RonSijm.Slopsidian.Linux;

public sealed record NativeThemeChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public static class NativeAppearance
{
    public static ThemeVariant GradientDark { get; } = new("GradientDark", ThemeVariant.Dark);
    public static ThemeVariant GradientLight { get; } = new("GradientLight", ThemeVariant.Light);
    public static ThemeVariant Matrix { get; } = new("Matrix", ThemeVariant.Dark);
    public static IReadOnlyList<NativeThemeChoice> Choices { get; } =
    [
        new("dark", "Dark"),
        new("light", "Light"),
        new("system", "System"),
        new("gradient-dark", "Gradient dark"),
        new("gradient-light", "Gradient light"),
        new("matrix", "Matrix"),
    ];

    public static ThemeVariant VariantFor(string theme) =>
        theme switch
        {
            "light" => ThemeVariant.Light,
            "system" => ThemeVariant.Default,
            "gradient-dark" => GradientDark,
            "gradient-light" => GradientLight,
            "matrix" => Matrix,
            _ => ThemeVariant.Dark,
        };

    public static bool IsLight(ThemeVariant variant) =>
        variant == ThemeVariant.Light || variant.InheritVariant == ThemeVariant.Light;

    public static Color DefaultAccent(ThemeVariant variant)
    {
        if (variant == Matrix)
        {
            return Color.Parse("#00FF41");
        }
        return Color.Parse(IsLight(variant) ? "#7051A8" : "#A68AF9");
    }

    public static bool TryParseAccent(string? value, out Color color)
    {
        color = default;
        return value is { Length: 7 } && value[0] == '#' && Color.TryParse(value, out color);
    }

    public static string FormatAccent(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    // Text accents must remain readable on the darkest/lightest native pane.
    public static Color ReadableAccent(Color color, ThemeVariant variant)
    {
        var background = Color.Parse(IsLight(variant) ? "#E5E5E5" : "#2D2D2D");
        if (variant == Matrix)
        {
            background = Color.Parse("#0E1B11");
        }
        return EnsureContrast(color, background, IsLight(variant) ? Colors.Black : Colors.White);
    }

    public static Color ControlAccent(Color color) => EnsureContrast(color, Colors.White, Colors.Black);

    private static Color EnsureContrast(Color color, Color background, Color target)
    {
        if (Contrast(color, background) >= 4.5)
        {
            return color;
        }
        var low = 0d;
        var high = 1d;
        for (var step = 0; step < 20; step++)
        {
            var middle = (low + high) / 2;
            if (Contrast(Blend(color, target, middle), background) >= 4.5)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }
        return Blend(color, target, high);
    }

    public static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return Linear(color.R) * 0.2126 + Linear(color.G) * 0.7152 + Linear(color.B) * 0.0722;
    }

    private static Color Blend(Color color, Color target, double amount) =>
        Color.FromRgb(
            (byte)Math.Round(color.R + (target.R - color.R) * amount),
            (byte)Math.Round(color.G + (target.G - color.G) * amount),
            (byte)Math.Round(color.B + (target.B - color.B) * amount)
        );
}
