using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class TerminalWindowTests : IDisposable
{
    private static readonly string[] ShellArguments = ["--noprofile", "--norc", "-i"];
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian terminal " + Guid.NewGuid().ToString("N")
    );
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public TerminalWindowTests() => Directory.CreateDirectory(directory);

    [AvaloniaFact]
    public async Task RealPtyUsesStartingDirectoryUnicodeInputInterruptAndExplicitRestart()
    {
        var window = new NativeTerminalWindow(directory, "/bin/bash", ShellArguments);
        var output = new List<byte>();
        window.Terminal.OutputReceived += (_, args) => output.AddRange(args.Bytes.ToArray());
        window.Show();
        try
        {
            await window.StartSessionAsync();
            Assert.True(window.Terminal.IsLive);
            await window.Terminal.SendInputAsync(
                "printf '\\120\\124\\131:'; test -t 0 && pwd; printf '\\303\\251\\342\\234\\223\\n'\r",
                Cancellation
            );
            await UntilAsync(() =>
                Encoding.UTF8.GetString(output.ToArray()).Contains("PTY:" + directory, StringComparison.Ordinal)
            );
            await UntilAsync(() => Encoding.UTF8.GetString(output.ToArray()).Contains("é✓", StringComparison.Ordinal));
            await window.Terminal.SendInputAsync("sleep 30\r", Cancellation);
            await Task.Delay(100, Cancellation);
            await window.Terminal.SendInputAsync(
                "\u0003printf '\\111\\116\\124\\105\\122\\122\\125\\120\\124\\105\\104\\n'\r",
                Cancellation
            );
            await UntilAsync(() =>
                Encoding.UTF8.GetString(output.ToArray()).Contains("INTERRUPTED", StringComparison.Ordinal)
            );
            var process = window.Terminal.Pid;
            window.EndSessionNow();
            await UntilAsync(() => !Alive(process));
            Assert.False(window.Terminal.IsLive);
            var previous = window.Terminal;
            await window.StartSessionAsync();
            Assert.NotSame(previous, window.Terminal);
            Assert.True(window.Terminal.IsLive);
            process = window.Terminal.Pid;
            await window.Terminal.SendInputAsync("exit 7\r", Cancellation);
            await UntilAsync(() => !window.Terminal.IsLive);
            await UntilAsync(() => !Alive(process));
            await UntilAsync(() =>
                ((NativeTerminalLayout)window.Content!)
                    .FindControl<TextBlock>("TerminalStatus")!
                    .Text?.Contains("exit 7", StringComparison.Ordinal) == true
            );
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.Terminal.IsLive);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task DisablingTerminalStopsItsPtyAndKeepsAssistantAvailable()
    {
        using var profile = new NativeProfile();
        var main = new MainWindow(profile);
        main.Show();
        try
        {
            await main.OpenVaultAsync(directory);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            var terminal = Assert.Single(main.OwnedWindows.OfType<NativeTerminalWindow>());
            Assert.True(
                terminal.Terminal.IsLive,
                ((NativeTerminalLayout)terminal.Content!).FindControl<TextBlock>("TerminalStatus")!.Text
            );
            var pid = terminal.Terminal.Pid;
            await main.SetNativeModuleEnabledAsync(NativeModule.Terminal, false);
            await UntilAsync(() => !Alive(pid));
            Assert.False(terminal.IsVisible);
            Assert.False(main.FindControl<Button>("TerminalButton")!.IsVisible);
            Assert.True(main.FindControl<Button>("AssistantButton")!.IsVisible);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            Assert.Empty(main.OwnedWindows.OfType<NativeTerminalWindow>());
            Assert.True(main.FindControl<Border>("ErrorPanel")!.IsVisible);
            await main.SetNativeModuleEnabledAsync(NativeModule.Terminal, true);
            Assert.Empty(main.OwnedWindows);
            await main.OpenTerminalAsync("/bin/bash", ShellArguments);
            terminal = Assert.Single(main.OwnedWindows.OfType<NativeTerminalWindow>());
            pid = terminal.Terminal.Pid;
            main.Close();
            await UntilAsync(() => !Alive(pid));
        }
        finally
        {
            if (main.IsVisible)
            {
                main.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task InvalidStartingDirectoryShowsRecoverableErrorWithoutStartingAShell()
    {
        var window = new NativeTerminalWindow(Path.Combine(directory, "missing"), "/bin/bash", ShellArguments);
        window.Show();
        try
        {
            await window.StartSessionAsync();
            Assert.False(window.Terminal.IsLive);
            Assert.Contains(
                "starting directory",
                ((NativeTerminalLayout)window.Content!).FindControl<TextBlock>("TerminalStatus")!.Text
            );
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ShellSelectionUsesAnExistingAbsoluteExecutablePath()
    {
        Assert.Equal("/bin/sh", NativeTerminalWindow.SelectShell("/bin/sh"));
        Assert.Equal("/bin/bash", NativeTerminalWindow.SelectShell("bash -c bad"));
        Assert.Equal("/bin/bash", NativeTerminalWindow.SelectShell("/missing-shell"));
    }

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
