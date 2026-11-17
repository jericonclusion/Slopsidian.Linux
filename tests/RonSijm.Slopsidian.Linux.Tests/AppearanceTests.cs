using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class AppearanceTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian-appearance-" + Guid.NewGuid().ToString("N")
    );
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static App Current => (App)Application.Current!;

    public AppearanceTests() => Directory.CreateDirectory(directory);

    [AvaloniaTheory]
    [InlineData("gradient-dark")]
    [InlineData("gradient-light")]
    public void GradientPresetsStayMonochromeAndRetainReadableTextAndSolidTerminalSurfaces(string theme)
    {
        try
        {
            Current.ApplyAppearance(new() { Theme = theme });
            foreach (var key in new[] { "SurfaceBrush", "SidebarBrush", "RibbonBrush" })
            {
                var brush = Assert.IsType<LinearGradientBrush>(Resource(key));
                var stops = brush.GradientStops.Select(stop => stop.Color).ToArray();
                Assert.True(stops.Length >= 2);
                Assert.NotEqual(stops[0], stops[^1]);
                foreach (var stop in stops)
                {
                    Assert.Equal(stop.R, stop.G);
                    Assert.Equal(stop.G, stop.B);
                    Assert.True(
                        NativeAppearance.Contrast(((ISolidColorBrush)Resource("TextBrush")).Color, stop) >= 4.5
                    );
                }
            }
            Assert.IsAssignableFrom<ISolidColorBrush>(Resource("TerminalSurfaceBrush"));
            Assert.Equal(theme == "gradient-light", NativeAppearance.IsLight(Current.ActualThemeVariant));
        }
        finally
        {
            Current.ApplyAppearance(new());
        }
    }

    [AvaloniaFact]
    public void MatrixPresetUsesBlackAndGreenWithReadableSecondaryTextAndFluentAccent()
    {
        try
        {
            Current.ApplyAppearance(new() { Theme = "matrix" });
            Assert.Equal(NativeAppearance.Matrix, Current.ActualThemeVariant);
            Assert.Equal(Color.Parse("#00FF41"), ((ISolidColorBrush)Resource("AccentBrush")).Color);
            var selection = Current.Styles.OfType<FluentTheme>().Single().Palettes[ThemeVariant.Dark].Accent;
            Assert.True(selection.G > selection.R && selection.G > selection.B);
            Assert.True(NativeAppearance.Contrast(selection, Colors.White) >= 4.5);
            var background = ((ISolidColorBrush)Resource("SidebarBrush")).Color;
            var text = ((ISolidColorBrush)Resource("TextBrush")).Color;
            var muted = ((ISolidColorBrush)Resource("MutedBrush")).Color;
            Assert.True(background.R < 25 && background.G < 25 && background.B < 25);
            Assert.True(text.G > text.R && text.G > text.B);
            Assert.True(NativeAppearance.Contrast(text, background) >= 4.5);
            Assert.True(NativeAppearance.Contrast(muted, background) >= 4.5);
            Current.ApplyAppearance(new());
            Assert.Equal(
                Color.Parse("#8C70C9"),
                Current.Styles.OfType<FluentTheme>().Single().Palettes[ThemeVariant.Dark].Accent
            );
        }
        finally
        {
            Current.ApplyAppearance(new());
        }
    }

    [AvaloniaTheory]
    [InlineData("dark", "#000000", "#2D2D2D")]
    [InlineData("light", "#FFFFFF", "#E5E5E5")]
    [InlineData("gradient-dark", "#112211", "#2D2D2D")]
    [InlineData("gradient-light", "#FFFF00", "#E5E5E5")]
    [InlineData("matrix", "#000000", "#0E1B11")]
    public void CustomAccentsKeepTheirSelectedValueAndDeriveReadableTextShades(
        string theme,
        string selected,
        string surface
    )
    {
        try
        {
            var preferences = new NativePreferences { Theme = theme, AccentColor = selected };
            Current.ApplyAppearance(preferences);
            var actual = ((ISolidColorBrush)Resource("AccentBrush")).Color;
            Assert.True(NativeAppearance.Contrast(actual, Color.Parse(surface)) >= 4.5);
            Assert.Equal(selected, preferences.AccentColor);
            var selection = Current.Styles.OfType<FluentTheme>().Single().Palettes[ThemeVariant.Light].Accent;
            Assert.True(NativeAppearance.Contrast(selection, Colors.White) >= 4.5);
            Current.ApplyAppearance(new());
            Assert.Equal(Color.Parse("#A68AF9"), ((ISolidColorBrush)Resource("AccentBrush")).Color);
        }
        finally
        {
            Current.ApplyAppearance(new());
        }
    }

    [AvaloniaFact]
    public async Task SettingsPreviewThenCancelRestoresAppearanceAndKeepsImmediateModuleChoices()
    {
        using var profile = new NativeProfile();
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            var settings = OpenSettings(main);
            SelectTheme(settings, "gradient-light");
            Assert.Equal(NativeAppearance.GradientLight, Current.ActualThemeVariant);
            Find<ColorPicker>(settings, "AccentColorPicker").Color = Color.Parse("#E95420");
            Assert.Equal(Color.Parse("#E95420"), Find<ColorPicker>(settings, "AccentColorPicker").Color);
            Assert.True(
                NativeAppearance.Contrast(
                    Current.Styles.OfType<FluentTheme>().Single().Palettes[ThemeVariant.Light].Accent,
                    Colors.White
                ) >= 4.5
            );
            Find<ListBox>(settings, "SettingsSections").SelectedItem = "Native modules";
            var terminal = Find<CheckBox>(settings, "TerminalEnabled");
            terminal.IsChecked = false;
            await UntilAsync(() => !profile.State.Preferences.TerminalEnabled && terminal.IsEnabled);
            settings.Close();
            await UntilAsync(() => Current.ActualThemeVariant == ThemeVariant.Dark);
            Assert.Equal("dark", profile.State.Preferences.Theme);
            Assert.Null(profile.State.Preferences.AccentColor);
            Assert.False(profile.State.Preferences.TerminalEnabled);
        }
        finally
        {
            CloseAll(main);
            Current.ApplyAppearance(new());
        }
    }

    [AvaloniaFact]
    public async Task SettingsSaveThemeAndColorWithoutAVaultAndRestoreThemOnRestart()
    {
        var path = Path.Combine(directory, "profile");
        using var profile = new NativeProfile(path);
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            var settings = OpenSettings(main);
            Assert.Equal(6, Find<ComboBox>(settings, "ThemePicker").ItemsSource!.Cast<NativeThemeChoice>().Count());
            SelectTheme(settings, "matrix");
            var picker = Find<ColorPicker>(settings, "AccentColorPicker");
            Assert.False(picker.IsAlphaEnabled);
            Assert.False(picker.IsAlphaVisible);
            Assert.True(picker.IsColorComponentsVisible);
            Assert.True(picker.IsHexInputVisible);
            Assert.Equal(Color.Parse("#00FF41"), picker.Color);
            picker.Color = Color.Parse("#55CC88");
            Click(settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Done")));
            await UntilAsync(() => File.Exists(Path.Combine(path, "state.json")));
            using var restored = new NativeProfile(path);
            await restored.LoadAsync(Cancellation);
            Assert.Equal("matrix", restored.State.Preferences.Theme);
            Assert.Equal("#55CC88", restored.State.Preferences.AccentColor);
            Assert.Null(restored.State.Session);
            Current.ApplyAppearance(restored.State.Preferences);
            Assert.Equal(NativeAppearance.Matrix, Current.ActualThemeVariant);
        }
        finally
        {
            CloseAll(main);
            await UntilAsync(() => !main.IsVisible);
            Current.ApplyAppearance(new());
        }
    }

    [AvaloniaFact]
    public async Task ResetAccentUsesTheCurrentPresetDefaultAndSystemModeRemainsAvailable()
    {
        using var profile = new NativeProfile();
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            var settings = OpenSettings(main);
            SelectTheme(settings, "matrix");
            Find<ColorPicker>(settings, "AccentColorPicker").Color = Color.Parse("#CC4488");
            var reset = Find<Button>(settings, "ResetAccentButton");
            Assert.True(reset.IsEnabled);
            Click(reset);
            Assert.False(reset.IsEnabled);
            Assert.Equal("#00FF41", Find<TextBlock>(settings, "AccentColorValue").Text);
            Assert.Equal(Color.Parse("#00FF41"), ((ISolidColorBrush)Resource("AccentBrush")).Color);
            SelectTheme(settings, "system");
            Assert.Equal(ThemeVariant.Default, Current.RequestedThemeVariant);
            settings.Close();
            await UntilAsync(() => Current.ActualThemeVariant == ThemeVariant.Dark);
        }
        finally
        {
            CloseAll(main);
            Current.ApplyAppearance(new());
        }
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12")]
    [InlineData("#00112233")]
    [InlineData("#GG1122")]
    [InlineData("")]
    public async Task InvalidStoredAccentIsRejectedWithoutChangingTheProfile(string value)
    {
        var file = Path.Combine(directory, "state.json");
        var json = System.Text.Json.JsonSerializer.Serialize(
            new { version = 1, preferences = new { accentColor = value } }
        );
        await File.WriteAllTextAsync(file, json, Cancellation);
        using var profile = new NativeProfile(directory);
        await Assert.ThrowsAsync<InvalidDataException>(() => profile.LoadAsync(Cancellation));
        Assert.Equal(json, await File.ReadAllTextAsync(file, Cancellation));
    }

    [Fact]
    public async Task ExistingProfilesKeepTheirOriginalThemeAndDefaultAccent()
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "state.json"),
            "{\"version\":1,\"preferences\":{\"theme\":\"light\"}}",
            Cancellation
        );
        using var profile = new NativeProfile(directory);
        await profile.LoadAsync(Cancellation);
        Assert.Equal("light", profile.State.Preferences.Theme);
        Assert.Null(profile.State.Preferences.AccentColor);
    }

    [AvaloniaFact]
    public async Task ThemeChangesKeepAnExistingTerminalSessionAliveAndReadable()
    {
        var window = new NativeTerminalWindow(directory, "/bin/bash", ["--noprofile", "--norc", "-i"]);
        var bytes = new List<byte>();
        window.Terminal.OutputReceived += (_, args) => bytes.AddRange(args.Bytes.ToArray());
        window.Show();
        try
        {
            await window.StartSessionAsync();
            var pid = window.Terminal.Pid;
            foreach (var theme in new[] { "gradient-dark", "gradient-light", "matrix" })
            {
                Current.ApplyAppearance(new() { Theme = theme });
                Dispatcher.UIThread.RunJobs();
                Assert.True(window.Terminal.IsLive);
                Assert.Equal(pid, window.Terminal.Pid);
                var background = Assert.IsAssignableFrom<ISolidColorBrush>(window.Terminal.Background).Color;
                var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(window.Terminal.Foreground).Color;
                Assert.True(NativeAppearance.Contrast(foreground, background) >= 4.5);
            }
            await window.Terminal.SendInputAsync(
                "printf '\\124\\110\\105\\115\\105\\123\\117\\113\\n'\r",
                Cancellation
            );
            await UntilAsync(() =>
                System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("THEMESOK", StringComparison.Ordinal)
            );
        }
        finally
        {
            window.Close();
            Current.ApplyAppearance(new());
        }
    }

    private static object Resource(string key)
    {
        Assert.True(Current.Resources.TryGetResource(key, Current.ActualThemeVariant, out var value));
        return value!;
    }

    private static NativeSettingsWindow OpenSettings(MainWindow main)
    {
        main.KeyPressQwerty(PhysicalKey.Comma, RawInputModifiers.Control);
        main.KeyReleaseQwerty(PhysicalKey.Comma, RawInputModifiers.Control);
        var settings = Assert.Single(main.OwnedWindows.OfType<NativeSettingsWindow>());
        Find<ListBox>(settings, "SettingsSections").SelectedItem = "Appearance";
        return settings;
    }

    private static void SelectTheme(Window settings, string id) =>
        Find<ComboBox>(settings, "ThemePicker").SelectedItem = NativeAppearance.Choices.Single(choice =>
            choice.Id == id
        );

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void CloseAll(MainWindow main)
    {
        foreach (var window in main.OwnedWindows.ToArray())
        {
            window.Close();
        }
        main.Close();
    }

    private async Task UntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; !predicate() && attempt < 300; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, Cancellation);
        }
        Assert.True(predicate());
    }

    public void Dispose() => Directory.Delete(directory, true);
}
