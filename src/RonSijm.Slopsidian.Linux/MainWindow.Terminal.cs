using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RonSijm.Slopsidian.Linux;

public partial class MainWindow
{
    private NativeTerminalWindow? terminalWindow;
    public NativeTerminalPane? TerminalPane { get; private set; }

    private async void OpenTerminal(object? sender, RoutedEventArgs args) => await OpenTerminalAsync();

    public Task OpenTerminalAsync() => OpenTerminalAsync(null, null);

    public Task OpenTerminalAsync(string? shell, IReadOnlyList<string>? arguments) =>
        RunAsync(async () =>
        {
            if (!profile.State.Preferences.TerminalEnabled)
            {
                throw new NativeActionRequiredException(
                    "Enable Terminal in Settings → Native modules first.",
                    NativeErrorAction.EnableTerminal
                );
            }
            if (TerminalPane is not null)
            {
                terminalWindow?.Activate();
                TerminalPane.Terminal.Focus();
                return;
            }
            var directory = Workspace.Snapshot.Root ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var pane = new NativeTerminalPane(directory, shell, arguments);
            TerminalPane = pane;
            pane.PresentationRequested += async (_, _) => await SetTerminalDockedAsync(!pane.IsDocked);
            pane.CloseRequested += (_, _) =>
            {
                if (pane.IsDocked)
                {
                    CloseTerminal();
                }
            };
            PresentTerminal(profile.State.Preferences.TerminalDocked);
            await pane.StartSessionAsync();
        });

    public Task SetTerminalDockedAsync(bool docked) =>
        RunAsync(async () =>
        {
            var pane = TerminalPane;
            if (pane is null || pane.IsDocked == docked)
            {
                return;
            }
            var previous = profile.State.Preferences.TerminalDocked;
            profile.State = profile.State with
            {
                Preferences = profile.State.Preferences with { TerminalDocked = docked },
            };
            try
            {
                await PersistProfileAsync();
            }
            catch
            {
                profile.State = profile.State with
                {
                    Preferences = profile.State.Preferences with { TerminalDocked = previous },
                };
                throw;
            }
            if (ReferenceEquals(TerminalPane, pane))
            {
                PresentTerminal(docked);
            }
        });

    private void PresentTerminal(bool docked)
    {
        var pane = TerminalPane!;
        pane.Terminal.BeginReparent();
        try
        {
            if (terminalWindow is { } oldWindow)
            {
                terminalWindow = null;
                oldWindow.ReleasePane();
                oldWindow.Close();
            }
            TerminalDockHost.Content = null;
            TerminalDockHost.IsVisible = docked;
            TerminalDockSplitter.IsVisible = docked;
            WorkspaceGrid.RowDefinitions[1].Height = new GridLength(docked ? 4 : 0);
            WorkspaceGrid.RowDefinitions[2].MinHeight = docked ? 160 : 0;
            WorkspaceGrid.RowDefinitions[2].Height = new GridLength(0);
            pane.SetDocked(docked);
            if (docked)
            {
                TerminalDockHost.Content = pane;
                AdaptTerminalDock();
                UpdateLayout();
            }
            else
            {
                var window = new NativeTerminalWindow(pane);
                terminalWindow = window;
                window.Closed += (_, _) =>
                {
                    if (terminalWindow == window)
                    {
                        terminalWindow = null;
                        TerminalPane = null;
                    }
                };
                window.Show(this);
            }
            pane.Terminal.Focus();
        }
        finally
        {
            pane.Terminal.EndReparent();
        }
    }

    private void AdaptTerminalDock()
    {
        if (TerminalPane?.IsDocked != true)
        {
            return;
        }
        var maximum = Math.Max(160, WorkspaceGrid.Bounds.Height - 304);
        var row = WorkspaceGrid.RowDefinitions[2];
        row.MaxHeight = maximum;
        row.Height = new GridLength(Math.Clamp(profile.State.Preferences.TerminalDockHeight, 160, maximum));
    }

    private async void TerminalDockResized(object? sender, VectorEventArgs args)
    {
        if (TerminalPane?.IsDocked != true)
        {
            return;
        }
        profile.State = profile.State with
        {
            Preferences = profile.State.Preferences with
            {
                TerminalDockHeight = WorkspaceGrid.RowDefinitions[2].ActualHeight,
            },
        };
        await PersistProfileSafelyAsync();
    }

    public void CloseTerminal()
    {
        var pane = TerminalPane;
        TerminalPane = null;
        terminalWindow?.Close();
        terminalWindow = null;
        TerminalDockHost.Content = null;
        TerminalDockHost.IsVisible = false;
        TerminalDockSplitter.IsVisible = false;
        WorkspaceGrid.RowDefinitions[1].Height = new GridLength(0);
        WorkspaceGrid.RowDefinitions[2].MinHeight = 0;
        WorkspaceGrid.RowDefinitions[2].Height = new GridLength(0);
        pane?.Dispose();
    }
}
