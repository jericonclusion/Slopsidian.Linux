using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class TerminalDockTests : IDisposable
{
    private static readonly string[] ShellArguments = ["--noprofile", "--norc", "-i"];
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian-dock-" + Guid.NewGuid().ToString("N")
    );
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public TerminalDockTests() => Directory.CreateDirectory(directory);

    [AvaloniaFact]
    public async Task DockAndWindowKeepTheSameShellBufferAndWorkingDirectory()
    {
        using var profile = new NativeProfile();
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenVaultAsync(directory);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            var pane = Assert.IsType<NativeTerminalPane>(main.TerminalPane);
            var terminal = pane.Terminal;
            var emulator = terminal.Terminal;
            var pid = terminal.Pid;
            var output = new List<byte>();
            terminal.OutputReceived += (_, args) => output.AddRange(args.Bytes.ToArray());
            Assert.True(pane.IsDocked);
            Assert.Empty(main.OwnedWindows);
            await terminal.SendInputAsync(
                "proof=transfer; mkdir session; cd session; printf 'REA%s\\n' DY\r",
                Cancellation
            );
            await UntilAsync(() => Text(output).Contains("READY", StringComparison.Ordinal));
            for (var move = 0; move < 4; move++)
            {
                await main.SetTerminalDockedAsync(move % 2 != 0);
                Assert.Same(pane, main.TerminalPane);
                Assert.Same(terminal, pane.Terminal);
                Assert.Same(emulator, terminal.Terminal);
                Assert.True(terminal.IsLive);
                Assert.Equal(pid, terminal.Pid);
                if (pane.IsDocked)
                {
                    Assert.Empty(main.OwnedWindows);
                    Assert.Same(pane, main.FindControl<ContentControl>("TerminalDockHost")!.Content);
                }
                else
                {
                    Assert.Same(pane, Assert.Single(main.OwnedWindows.OfType<NativeTerminalWindow>()).Pane);
                }
                await terminal.SendInputAsync(
                    $"printf '%s:%s\\n' \"$proof\" \"$PWD\"; printf 'move-%s\\n' {move}\r",
                    Cancellation
                );
                await UntilAsync(() => Text(output).Contains("move-" + move, StringComparison.Ordinal));
                Assert.Contains("transfer:" + Path.Combine(directory, "session"), Text(output));
            }
            main.CloseTerminal();
            await UntilAsync(() => !Alive(pid));
        }
        finally
        {
            await CloseAsync(main);
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisableAndCloseEndTheShellInEitherPresentation(bool docked)
    {
        using var profile = new NativeProfile();
        profile.State = profile.State with { Preferences = new() { TerminalDocked = docked } };
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            var pane = Assert.IsType<NativeTerminalPane>(main.TerminalPane);
            var pid = pane.Terminal.Pid;
            await main.SetNativeModuleEnabledAsync(NativeModule.Terminal, false);
            await UntilAsync(() => !Alive(pid));
            Assert.Null(main.TerminalPane);
            Assert.Empty(main.OwnedWindows);
            Assert.False(main.FindControl<ContentControl>("TerminalDockHost")!.IsVisible);
            await main.SetNativeModuleEnabledAsync(NativeModule.Terminal, true);
            Assert.Null(main.TerminalPane);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            pane = Assert.IsType<NativeTerminalPane>(main.TerminalPane);
            pid = pane.Terminal.Pid;
            ((NativeTerminalLayout)pane.Content!)
                .FindControl<Button>("CloseTerminalButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => !Alive(pid));
            Assert.Null(main.TerminalPane);
            Assert.Empty(main.OwnedWindows);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            pid = main.TerminalPane!.Terminal.Pid;
            await CloseAsync(main);
            await UntilAsync(() => !Alive(pid));
        }
        finally
        {
            await CloseAsync(main);
        }
    }

    [AvaloniaFact]
    public async Task DockPreferencePersistsWithoutStartingAShellOnNextLaunch()
    {
        var profilePath = Path.Combine(directory, "profile");
        using var profile = new NativeProfile(profilePath);
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            var layout = (NativeTerminalLayout)main.TerminalPane!.Content!;
            layout.FindControl<Button>("DockButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => !main.TerminalPane!.IsDocked);
            using var restored = new NativeProfile(profilePath);
            await restored.LoadAsync(Cancellation);
            Assert.False(restored.State.Preferences.TerminalDocked);
            var next = new MainWindow(restored);
            next.Show();
            try
            {
                Assert.Null(next.TerminalPane);
                await next.OpenTerminalAsync("/bin/bash", ShellArguments);
                Assert.False(next.TerminalPane!.IsDocked);
                Assert.Single(next.OwnedWindows.OfType<NativeTerminalWindow>());
            }
            finally
            {
                await CloseAsync(next);
            }
        }
        finally
        {
            await CloseAsync(main);
        }
    }

    [AvaloniaFact]
    public async Task FailedPreferenceSaveLeavesTheLiveDockAndShellUntouched()
    {
        var blocked = Path.Combine(directory, "blocked-profile");
        await File.WriteAllTextAsync(blocked, "blocks directory creation", Cancellation);
        using var profile = new NativeProfile(blocked);
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            var pane = main.TerminalPane!;
            var pid = pane.Terminal.Pid;
            await main.SetTerminalDockedAsync(false);
            Assert.True(pane.IsDocked);
            Assert.True(profile.State.Preferences.TerminalDocked);
            Assert.True(pane.Terminal.IsLive);
            Assert.Equal(pid, pane.Terminal.Pid);
            Assert.Empty(main.OwnedWindows);
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
        }
        finally
        {
            File.Delete(blocked);
            await CloseAsync(main);
        }
    }

    [AvaloniaFact]
    public async Task MinimumWindowKeepsEditorAndTerminalControlsUsableAndShellOwnsItsShortcuts()
    {
        using var profile = new NativeProfile();
        profile.State = profile.State with { Preferences = new() { TerminalDockHeight = 900 } };
        var main = new MainWindow(profile) { Width = 760, Height = 520 };
        main.Show();
        try
        {
            await main.OpenVaultAsync(directory);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            main.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var pane = main.TerminalPane!;
            Assert.True(main.FindControl<Grid>("DocumentArea")!.Bounds.Height >= 299);
            var layout = (NativeTerminalLayout)pane.Content!;
            var close = layout.FindControl<Button>("CloseTerminalButton")!;
            var edge = close.TranslatePoint(new Point(close.Bounds.Width, close.Bounds.Height), main)!.Value;
            Assert.True(edge.X <= main.ClientSize.Width && edge.Y <= main.ClientSize.Height);
            Assert.True(pane.Terminal.Bounds.Height >= 40);
            pane.Terminal.Focus();
            Assert.True(pane.Terminal.IsKeyboardFocusWithin);
            main.KeyPressQwerty(PhysicalKey.O, RawInputModifiers.Control);
            main.KeyReleaseQwerty(PhysicalKey.O, RawInputModifiers.Control);
            Assert.Empty(main.OwnedWindows);
            Assert.True(pane.Terminal.IsLive);
        }
        finally
        {
            await CloseAsync(main);
        }
    }

    private static string Text(List<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray());

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task CloseAsync(MainWindow window)
    {
        await UntilAsync(() =>
        {
            if (window.IsVisible)
            {
                window.Close();
            }
            return !window.IsVisible;
        });
    }

    private async Task UntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; !predicate() && attempt < 500; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, Cancellation);
        }
        Assert.True(predicate());
    }

    public void Dispose() => Directory.Delete(directory, true);
}
