using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace RonSijm.Slopsidian.Linux;

public partial class App : Application
{
    private Color? customAccent;
    public event EventHandler? AppearanceChanged;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ActualThemeVariantChanged += (_, _) => ApplyAccent();
    }

    public void ApplyAppearance(NativePreferences preferences)
    {
        customAccent = NativeAppearance.TryParseAccent(preferences.AccentColor, out var color) ? color : null;
        RequestedThemeVariant = NativeAppearance.VariantFor(preferences.Theme);
        ApplyAccent();
    }

    private void ApplyAccent()
    {
        var theme = Styles.OfType<FluentTheme>().Single();
        var matrixAccent = ActualThemeVariant == NativeAppearance.Matrix ? Color.Parse("#00FF41") : (Color?)null;
        var darkAccent = customAccent ?? matrixAccent;
        theme.Palettes[ThemeVariant.Dark].Accent = darkAccent is { } dark
            ? NativeAppearance.ControlAccent(dark)
            : Color.Parse("#8C70C9");
        theme.Palettes[ThemeVariant.Light].Accent = customAccent is { } light
            ? NativeAppearance.ControlAccent(light)
            : Color.Parse("#7051A8");
        if (customAccent is { } color)
        {
            Resources["AccentBrush"] = new SolidColorBrush(NativeAppearance.ReadableAccent(color, ActualThemeVariant));
        }
        else
        {
            Resources.Remove("AccentBrush");
        }
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var profileArgument = desktop.Args?.FirstOrDefault(value =>
                value.StartsWith("--profile=", StringComparison.Ordinal)
            );
            var profilePath = profileArgument is null
                ? NativeProfile.DefaultDirectory
                : Path.GetFullPath(profileArgument["--profile=".Length..]);
            var window = new MainWindow(new NativeProfile(profilePath));
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                var argument = desktop.Args?.FirstOrDefault(value =>
                    value.StartsWith("--vault=", StringComparison.Ordinal)
                );
                await window.InitializeAsync(argument?["--vault=".Length..]);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
