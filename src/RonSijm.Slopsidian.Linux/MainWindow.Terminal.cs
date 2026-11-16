using Avalonia.Interactivity;

namespace RonSijm.Slopsidian.Linux;

public partial class MainWindow
{
    private NativeTerminalWindow? terminalWindow;

    private async void OpenTerminal(object? sender, RoutedEventArgs args) => await OpenTerminalAsync();

    public Task OpenTerminalAsync() => OpenTerminalAsync(null, null);

    public Task OpenTerminalAsync(string? shell, IReadOnlyList<string>? arguments) =>
        RunAsync(async () =>
        {
            if (!profile.State.Preferences.TerminalEnabled)
            {
                throw new InvalidOperationException("Enable Terminal in Settings → Native modules first.");
            }
            if (terminalWindow is not null)
            {
                terminalWindow.Activate();
                return;
            }
            var directory = Workspace.Snapshot.Root ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var window = new NativeTerminalWindow(directory, shell, arguments);
            terminalWindow = window;
            window.Closed += (_, _) =>
            {
                if (terminalWindow == window)
                {
                    terminalWindow = null;
                }
            };
            window.Show(this);
            await window.StartSessionAsync();
        });
}
