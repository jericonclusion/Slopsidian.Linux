using Avalonia.Controls;
using Iciclecreek.Terminal;

namespace RonSijm.Slopsidian.Linux;

/// <summary>A detachable host. The pane owns the shell and can move without restarting it.</summary>
public sealed class NativeTerminalWindow : Window
{
    public NativeTerminalWindow(string directory, string? shell = null, IReadOnlyList<string>? arguments = null)
        : this(new NativeTerminalPane(directory, shell, arguments)) { }

    internal NativeTerminalWindow(NativeTerminalPane pane)
    {
        Pane = pane;
        Title = "Terminal — Slopsidian Linux";
        Width = 1000;
        Height = 700;
        MinWidth = 600;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = pane;
        pane.CloseRequested += CloseRequested;
        Closed += (_, _) =>
        {
            pane.CloseRequested -= CloseRequested;
            if (ReferenceEquals(Content, pane))
            {
                pane.Dispose();
            }
        };
    }

    public NativeTerminalPane Pane { get; }
    public TerminalControl Terminal => Pane.Terminal;

    public static string SelectShell(string? configured) => NativeTerminalPane.SelectShell(configured);

    public Task StartSessionAsync() => Pane.StartSessionAsync();

    public void EndSessionNow() => Pane.EndSessionNow();

    private void CloseRequested(object? sender, EventArgs args) => Close();

    internal void ReleasePane()
    {
        Pane.CloseRequested -= CloseRequested;
        Content = null;
    }
}
