using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class NativeModuleTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian-modules-" + Guid.NewGuid().ToString("N")
    );
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public NativeModuleTests() => Directory.CreateDirectory(directory);

    [AvaloniaFact]
    public async Task ModuleChoicesPersistWithoutAVaultAndKeepProviderSettings()
    {
        var path = Path.Combine(directory, "profile");
        using var profile = new NativeProfile(path);
        profile.State = profile.State with { Assistant = new() { Codex = new("/custom/codex", "model") } };
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.SetNativeModuleEnabledAsync(NativeModule.CliAssistant, false);
            await main.SetNativeModuleEnabledAsync(NativeModule.Terminal, false);
            using var restored = new NativeProfile(path);
            await restored.LoadAsync(Cancellation);
            Assert.False(restored.State.Preferences.CliAssistantEnabled);
            Assert.False(restored.State.Preferences.TerminalEnabled);
            Assert.Equal(profile.State.Assistant, restored.State.Assistant);
            Assert.Null(restored.State.Session);
            Assert.Empty(main.OwnedWindows);
            await main.SetNativeModuleEnabledAsync(NativeModule.CliAssistant, true);
            Assert.Empty(main.OwnedWindows);
        }
        finally
        {
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task SettingsDisableClosesAssistantStopsRequestAndRemovesItsCommandsWithoutTouchingVaultConfig()
    {
        var vault = Path.Combine(directory, "vault");
        Directory.CreateDirectory(Path.Combine(vault, ".obsidian"));
        var config = Path.Combine(vault, ".obsidian", "core-plugins.json");
        await File.WriteAllTextAsync(config, "[\"unknown-plugin\",\"graph\"]", Cancellation);
        var executable = Path.Combine(directory, "fake cli");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\ncat >/dev/null\nsleep 30\n", Cancellation);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        using var profile = new NativeProfile(Path.Combine(directory, "profile"));
        profile.State = profile.State with { Assistant = new() { Codex = new(executable) } };
        var main = new MainWindow(profile);
        main.Show();
        NativeSettingsWindow? settings = null;
        try
        {
            await main.OpenVaultAsync(vault);
            await main.OpenAssistantAsync();
            var assistant = Assert.Single(main.OwnedWindows.OfType<NativeAssistantWindow>());
            ((NativeAssistantLayout)assistant.Content!).FindControl<TextBox>("PromptBox")!.Text = "Wait until disabled";
            var pending = assistant.SendAsync();
            Assert.True(assistant.IsRunning);
            settings = new NativeSettingsWindow(
                profile.State.Preferences,
                main.Workspace.Vault,
                main.SetNativeModuleEnabledAsync
            );
            settings.Show(main);
            Find<ListBox>(settings, "SettingsSections").SelectedItem = "Native modules";
            Find<CheckBox>(settings, "CliAssistantEnabled").IsChecked = false;
            await UntilAsync(() => !profile.State.Preferences.CliAssistantEnabled && !assistant.IsVisible);
            await pending.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            Assert.False(assistant.IsRunning);
            Assert.False(main.FindControl<Button>("AssistantButton")!.IsVisible);
            Assert.True(main.FindControl<Button>("TerminalButton")!.IsVisible);
            settings.Close();
            settings = null;
            main.KeyPressQwerty(PhysicalKey.P, RawInputModifiers.Control);
            main.KeyReleaseQwerty(PhysicalKey.P, RawInputModifiers.Control);
            var picker = Assert.Single(main.OwnedWindows.OfType<NativePicker>());
            var choices = picker
                .GetVisualDescendants()
                .OfType<ListBox>()
                .Single()
                .ItemsSource!.Cast<NativeChoice>()
                .ToArray();
            Assert.DoesNotContain(choices, choice => choice.Value == "assistant:open");
            Assert.Contains(choices, choice => choice.Value == "terminal:open");
            picker.Close();
            await main.OpenAssistantAsync();
            Assert.Empty(main.OwnedWindows.OfType<NativeAssistantWindow>());
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Assert.Equal("[\"unknown-plugin\",\"graph\"]", await File.ReadAllTextAsync(config, Cancellation));
            await main.SetNativeModuleEnabledAsync(NativeModule.CliAssistant, true);
            Assert.True(main.FindControl<Button>("AssistantButton")!.IsVisible);
            Assert.Empty(main.OwnedWindows);
        }
        finally
        {
            settings?.Close();
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task FailedSaveKeepsModuleAvailableAndRevertsSettingsWithAnError()
    {
        var path = Path.Combine(directory, "blocked profile");
        await File.WriteAllTextAsync(path, "block directory creation", Cancellation);
        using var profile = new NativeProfile(path);
        var main = new MainWindow(profile);
        main.Show();
        var settings = new NativeSettingsWindow(profile.State.Preferences, null, main.SetNativeModuleEnabledAsync);
        settings.Show(main);
        try
        {
            Find<ListBox>(settings, "SettingsSections").SelectedItem = "Native modules";
            var toggle = Find<CheckBox>(settings, "CliAssistantEnabled");
            toggle.IsChecked = false;
            await UntilAsync(() => !string.IsNullOrEmpty(Find<TextBlock>(settings, "SettingsError").Text));
            Assert.True(toggle.IsChecked);
            Assert.True(toggle.IsEnabled);
            Assert.True(profile.State.Preferences.CliAssistantEnabled);
            Assert.True(main.FindControl<Button>("AssistantButton")!.IsVisible);
        }
        finally
        {
            File.Delete(path);
            settings.Close();
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task SettingsWaitForPersistenceBeforeReenablingControls()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings = new NativeSettingsWindow(new(), null, (_, _) => completion.Task);
        settings.Show();
        try
        {
            Find<ListBox>(settings, "SettingsSections").SelectedItem = "Native modules";
            var toggle = Find<CheckBox>(settings, "TerminalEnabled");
            toggle.IsChecked = false;
            Assert.False(toggle.IsEnabled);
            Assert.False(Find<ListBox>(settings, "SettingsSections").IsEnabled);
            Assert.False(
                settings
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => Equals(button.Content, "Done"))
                    .IsEnabled
            );
            completion.SetResult();
            await UntilAsync(() => toggle.IsEnabled);
            Assert.False(toggle.IsChecked);
        }
        finally
        {
            completion.TrySetResult();
            settings.Close();
        }
    }

    [Fact]
    public async Task ProfilesCreatedBeforeModulesKeepBothFeaturesAvailable()
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "state.json"),
            "{\"version\":1,\"preferences\":{\"fontSize\":18}}",
            Cancellation
        );
        using var profile = new NativeProfile(directory);
        await profile.LoadAsync(Cancellation);
        Assert.True(profile.State.Preferences.CliAssistantEnabled);
        Assert.True(profile.State.Preferences.TerminalEnabled);
        Assert.Equal(18, profile.State.Preferences.FontSize);
    }

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

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
