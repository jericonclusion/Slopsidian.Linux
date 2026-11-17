using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class ActionableErrorTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian-error-action-" + Guid.NewGuid().ToString("N")
    );
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ActionableErrorTests() => Directory.CreateDirectory(directory);

    [AvaloniaFact]
    public async Task MissingVaultActionOpensChooserOnceKeepsErrorOnCancelAndOpensSelectedVault()
    {
        var main = new MainWindow();
        main.Show();
        try
        {
            await main.OpenAssistantAsync();
            Assert.Equal("Open a vault first.", main.FindControl<TextBlock>("ErrorText")!.Text);
            Assert.Equal("Open vault…", main.FindControl<TextBlock>("ErrorActionText")!.Text);
            var action = main.FindControl<Button>("ErrorActionButton")!;
            Assert.True(action.Focus());
            main.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            main.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            var dialog = Assert.Single(main.OwnedWindows.OfType<NativeVaultDialog>());
            Click(action);
            Assert.Single(main.OwnedWindows);
            Assert.False(action.IsEnabled);
            dialog.Close();
            await UntilAsync(() => action.IsEnabled);
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Click(action);
            dialog = Assert.Single(main.OwnedWindows.OfType<NativeVaultDialog>());
            dialog
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single(box => box.PlaceholderText == "/home/you/Notes")
                .Text = directory;
            Click(
                dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Open vault"))
            );
            await UntilAsync(() =>
                main.Workspace.Snapshot.Root == directory && !main.FindControl<Border>("ErrorPanel")!.IsVisible
            );
            Assert.False(action.IsVisible);
            Assert.Empty(main.OwnedWindows);
        }
        finally
        {
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task MissingVaultActionCanCreateANewVaultThroughTheChooser()
    {
        var main = new MainWindow();
        main.Show();
        try
        {
            await main.OpenAssistantAsync();
            Click(main.FindControl<Button>("ErrorActionButton")!);
            var dialog = Assert.Single(main.OwnedWindows.OfType<NativeVaultDialog>());
            dialog.GetVisualDescendants().OfType<TextBox>().Single(box => box.PlaceholderText == "My vault").Text =
                "Notes";
            dialog
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single(box => box.PlaceholderText == "Choose the parent folder")
                .Text = directory;
            Click(dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("create")));
            var created = Path.Combine(directory, "Notes");
            await UntilAsync(() =>
                main.Workspace.Snapshot.Root == created && !main.FindControl<Border>("ErrorPanel")!.IsVisible
            );
            Assert.True(Directory.Exists(created));
        }
        finally
        {
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task CoreVaultRequirementCarriesRecoveryMetadata()
    {
        var main = new MainWindow();
        main.Show();
        try
        {
            await main.RunAsync(async () =>
                await main.Workspace.Vault.ReadConfigurationAsync("app", null, cancellationToken: Cancellation)
            );
            Assert.True(main.FindControl<Button>("ErrorActionButton")!.IsVisible);
            Assert.Equal("Open vault…", main.FindControl<TextBlock>("ErrorActionText")!.Text);
            Click(main.FindControl<Button>("ErrorActionButton")!);
            Assert.Single(main.OwnedWindows.OfType<NativeVaultDialog>()).Close();
            await UntilAsync(() => main.FindControl<Button>("ErrorActionButton")!.IsEnabled);
        }
        finally
        {
            main.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(NativeModule.CliAssistant)]
    [InlineData(NativeModule.Terminal)]
    public async Task DisabledModuleActionSelectsNativeModulesAndClearsErrorAfterEnabling(NativeModule module)
    {
        using var profile = new NativeProfile();
        profile.State = profile.State with
        {
            Preferences = new() { CliAssistantEnabled = false, TerminalEnabled = false },
        };
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await (module == NativeModule.CliAssistant ? main.OpenAssistantAsync() : main.OpenTerminalAsync());
            Assert.Equal("Open settings…", main.FindControl<TextBlock>("ErrorActionText")!.Text);
            var action = main.FindControl<Button>("ErrorActionButton")!;
            Click(action);
            var settings = Assert.Single(main.OwnedWindows.OfType<NativeSettingsWindow>());
            Assert.Equal("Native modules", Find<ListBox>(settings, "SettingsSections").SelectedItem);
            settings.Close();
            await UntilAsync(() => action.IsEnabled);
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Click(action);
            settings = Assert.Single(main.OwnedWindows.OfType<NativeSettingsWindow>());
            Find<CheckBox>(settings, module + "Enabled").IsChecked = true;
            Click(settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Done")));
            await UntilAsync(() => !main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Assert.True(profile.State.Preferences.IsModuleEnabled(module));
            Assert.False(
                profile.State.Preferences.IsModuleEnabled(
                    module == NativeModule.CliAssistant ? NativeModule.Terminal : NativeModule.CliAssistant
                )
            );
            Assert.Empty(main.OwnedWindows);
        }
        finally
        {
            foreach (var window in main.OwnedWindows.ToArray())
            {
                window.Close();
            }
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task UnrelatedErrorsReplaceTheActionWithoutMatchingMessageTextAndDismissResetsIt()
    {
        var main = new MainWindow();
        main.Show();
        try
        {
            await main.OpenAssistantAsync();
            await main.RunAsync(() => throw new IOException("Open a vault first."));
            Assert.False(main.FindControl<Button>("ErrorActionButton")!.IsVisible);
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            await main.OpenAssistantAsync();
            Click(
                main.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => AutomationProperties.GetName(button) == "Dismiss error")
            );
            Assert.False(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Assert.False(main.FindControl<Button>("ErrorActionButton")!.IsVisible);
        }
        finally
        {
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task RecoveryActionIsDisabledWhileAnotherOperationRunsAndNormalSuccessClearsIt()
    {
        var main = new MainWindow();
        main.Show();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await main.OpenAssistantAsync();
            var pending = main.RunAsync(() => completion.Task, clearError: false);
            var action = main.FindControl<Button>("ErrorActionButton")!;
            Assert.False(action.IsEnabled);
            Click(action);
            Assert.Empty(main.OwnedWindows);
            completion.SetResult();
            await pending;
            Assert.True(action.IsEnabled);
            await main.RunAsync(() => Task.CompletedTask);
            Assert.False(action.IsVisible);
            Assert.False(main.FindControl<Border>("ErrorPanel")!.IsVisible);
        }
        finally
        {
            completion.TrySetResult();
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task SuccessfulRecoveryDoesNotClearANewerError()
    {
        using var profile = new NativeProfile();
        profile.State = profile.State with { Preferences = new() { CliAssistantEnabled = false } };
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenAssistantAsync();
            Click(main.FindControl<Button>("ErrorActionButton")!);
            var settings = Assert.Single(main.OwnedWindows.OfType<NativeSettingsWindow>());
            await main.RunAsync(() => throw new IOException("Newer failure"));
            Find<CheckBox>(settings, "CliAssistantEnabled").IsChecked = true;
            Click(settings.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Done")));
            await UntilAsync(() => !settings.IsVisible);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Newer failure", main.FindControl<TextBlock>("ErrorText")!.Text);
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            Assert.False(main.FindControl<Button>("ErrorActionButton")!.IsVisible);
        }
        finally
        {
            foreach (var window in main.OwnedWindows.ToArray())
            {
                window.Close();
            }
            main.Close();
        }
    }

    [AvaloniaFact]
    public async Task LongErrorsKeepRecoveryAndDismissReachableAtMinimumWindowSize()
    {
        var main = new MainWindow { Width = 760, Height = 520 };
        main.Show();
        try
        {
            await main.RunAsync(() =>
                throw new NativeActionRequiredException(
                    string.Join(" ", Enumerable.Repeat("A long synthetic diagnostic with a path.", 80)),
                    NativeErrorAction.OpenVault
                )
            );
            main.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            main.UpdateLayout();
            var action = main.FindControl<Button>("ErrorActionButton")!;
            Assert.True(action.IsVisible);
            Assert.True(action.Bounds.Width > 80);
            var bottom = action.TranslatePoint(new Point(0, action.Bounds.Height), main)!.Value.Y;
            Assert.InRange(bottom, 1, 150);
            Assert.InRange(main.FindControl<ScrollViewer>("ErrorMessageScroll")!.Bounds.Height, 1, 120);
            Assert.True(main.FindControl<Grid>("WorkspaceGrid")!.Bounds.Height > 250);
        }
        finally
        {
            main.Close();
        }
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
