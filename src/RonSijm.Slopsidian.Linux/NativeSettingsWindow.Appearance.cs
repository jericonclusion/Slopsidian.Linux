using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeSettingsWindow
{
    private void AppearancePage()
    {
        var theme = new ComboBox
        {
            Name = "ThemePicker",
            ItemsSource = NativeAppearance.Choices,
            SelectedItem = NativeAppearance.Choices.FirstOrDefault(choice => choice.Id == preferences.Theme)
                ?? NativeAppearance.Choices[0],
            Width = 240,
        };
        AddField("Theme", theme);
        page.Children.Add(
            new TextBlock
            {
                Text =
                    "Gradient themes use subtle monochrome shading. Matrix pairs near-black surfaces with green text and accents.",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            }
        );
        var picker = new ColorPicker
        {
            Name = "AccentColorPicker",
            Color = CurrentAccent(),
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            IsColorComponentsVisible = true,
            IsColorModelVisible = false,
            IsHexInputVisible = true,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(picker, "Accent color");
        var code = new TextBlock
        {
            Name = "AccentColorValue",
            Text = NativeAppearance.FormatAccent(picker.Color),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var reset = new Button
        {
            Name = "ResetAccentButton",
            Content = "Use theme default",
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { picker, code },
        };
        AddField("Accent color", row);
        page.Children.Add(reset);
        page.Children.Add(
            new TextBlock
            {
                Text =
                    "Choose a color or enter a hex value in the picker. Accent shades adapt for readability. Changes preview immediately; Done saves them.",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            }
        );
        var updating = false;
        void UpdatePicker()
        {
            updating = true;
            picker.Color = CurrentAccent();
            code.Text = NativeAppearance.FormatAccent(picker.Color);
            reset.IsEnabled = preferences.AccentColor is not null;
            updating = false;
        }
        picker.ColorChanged += (_, _) =>
        {
            if (updating)
            {
                return;
            }
            preferences = preferences with { AccentColor = NativeAppearance.FormatAccent(picker.Color) };
            code.Text = preferences.AccentColor;
            reset.IsEnabled = true;
            previewAppearance?.Invoke(preferences);
        };
        theme.SelectionChanged += (_, _) =>
        {
            preferences = preferences with { Theme = (theme.SelectedItem as NativeThemeChoice)?.Id ?? "dark" };
            previewAppearance?.Invoke(preferences);
            UpdatePicker();
        };
        reset.Click += (_, _) =>
        {
            preferences = preferences with { AccentColor = null };
            UpdatePicker();
            previewAppearance?.Invoke(preferences);
        };
        reset.IsEnabled = preferences.AccentColor is not null;
    }

    private Color CurrentAccent()
    {
        if (NativeAppearance.TryParseAccent(preferences.AccentColor, out var color))
        {
            return color;
        }
        var variant = NativeAppearance.VariantFor(preferences.Theme);
        if (variant == Avalonia.Styling.ThemeVariant.Default && Application.Current is { } app)
        {
            variant = app.ActualThemeVariant;
        }
        return NativeAppearance.DefaultAccent(variant);
    }
}
