using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Iciclecreek.Terminal;

namespace RonSijm.Slopsidian.Linux;

/// <summary>A native XTerm surface backed by a real local PTY, not a browser terminal.</summary>
public sealed class NativeTerminalPane : UserControl, IDisposable
{
    private readonly NativeTerminalLayout layout = new();
    private readonly string directory;
    private readonly string shell;
    private readonly string[] arguments;
    private bool closed;
    private bool starting;
    private bool launched;
    private Button NewSession => layout.FindControl<Button>("NewSessionButton")!;
    private Button EndSession => layout.FindControl<Button>("EndSessionButton")!;
    private TextBlock Status => layout.FindControl<TextBlock>("TerminalStatus")!;

    public NativeTerminalPane(string directory, string? shell = null, IReadOnlyList<string>? arguments = null)
    {
        this.directory = Path.GetFullPath(directory);
        this.shell = shell ?? SelectShell(Environment.GetEnvironmentVariable("SHELL"));
        this.arguments = arguments?.ToArray() ?? ["-i"];
        Content = layout;
        var label = layout.FindControl<TextBlock>("StartDirectory")!;
        label.Text = "Starts in: " + this.directory;
        ToolTip.SetTip(label, this.directory);
        Terminal = CreateTerminal();
        layout.FindControl<ContentControl>("TerminalHost")!.Content = Terminal;
        NewSession.Click += async (_, _) => await StartSessionAsync();
        EndSession.Click += (_, _) => EndSessionNow();
        layout.FindControl<Button>("CopyButton")!.Click += async (_, _) => await ClipboardActionAsync(false);
        layout.FindControl<Button>("PasteButton")!.Click += async (_, _) => await ClipboardActionAsync(true);
        layout.FindControl<Button>("DockButton")!.Click += (_, _) =>
            PresentationRequested?.Invoke(this, EventArgs.Empty);
        layout.FindControl<Button>("CloseTerminalButton")!.Click += (_, _) =>
            CloseRequested?.Invoke(this, EventArgs.Empty);
        UpdateActions();
    }

    public event EventHandler? PresentationRequested;
    public event EventHandler? CloseRequested;
    public TerminalControl Terminal { get; private set; }
    public bool IsDocked { get; private set; }

    internal void SetDocked(bool docked)
    {
        IsDocked = docked;
        layout.FindControl<Button>("DockButton")!.IsVisible = true;
        var dockButton = layout.FindControl<Button>("DockButton")!;
        var action = docked ? "Open terminal in a window" : "Dock terminal at bottom";
        AutomationProperties.SetName(dockButton, action);
        ToolTip.SetTip(dockButton, action);
        layout.FindControl<TextBlock>("DockActionText")!.Text = docked ? "Open in window" : "Dock at bottom";
        layout.FindControl<NativeIcon>("DockActionIcon")!.Kind = docked ? "pop-out" : "panel-bottom";
    }

    public void Dispose()
    {
        if (closed)
        {
            return;
        }
        closed = true;
        Terminal.Dispose();
        GC.SuppressFinalize(this);
    }

    public static string SelectShell(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) && File.Exists(configured))
        {
            return configured;
        }
        return File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
    }

    private TerminalControl CreateTerminal()
    {
        var control = new TerminalControl
        {
            Process = "", // Only an explicit StartSessionAsync launches a process.
            FontFamily = new FontFamily("DejaVu Sans Mono, monospace"),
            FontSize = 14,
            BufferSize = 2000,
        };
        control.Bind(TemplatedControl.BackgroundProperty, new DynamicResourceExtension("TerminalSurfaceBrush"));
        control.Bind(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("TextBrush"));
        control.Bind(TerminalControl.CursorColorProperty, new Binding("Foreground.Color") { Source = control });
        control.ProcessExited += (_, args) =>
        {
            if (!closed && ReferenceEquals(Terminal, control))
            {
                SetStatus($"Session finished (exit {args.ExitCode}). Choose New session to start again.");
                UpdateActions();
            }
        };
        return control;
    }

    public async Task StartSessionAsync()
    {
        if (closed || starting || Terminal.IsLive)
        {
            return;
        }
        starting = true;
        UpdateActions();
        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("The starting directory no longer exists.");
            }
            if (launched)
            {
                Terminal.Dispose();
                Terminal = CreateTerminal();
                layout.FindControl<ContentControl>("TerminalHost")!.Content = Terminal;
            }
            launched = true;
            SetStatus("Starting shell…");
            TopLevel.GetTopLevel(this)?.UpdateLayout();
            await Terminal.LaunchProcess(directory, shell, arguments);
            // Loaded must never automatically relaunch an exited or failed shell.
            Terminal.Process = "";
            if (!closed)
            {
                SetStatus(
                    Terminal.IsLive
                        ? "Interactive shell · closing the terminal ends this session."
                        : "Shell did not stay open. Check the terminal output, then choose New session."
                );
                Terminal.Focus();
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!closed)
            {
                SetStatus(exception.Message, true);
            }
        }
        finally
        {
            starting = false;
            if (!closed)
            {
                UpdateActions();
            }
        }
    }

    public void EndSessionNow()
    {
        using var connection = Terminal.DetachConnection();
        connection?.Kill();
        SetStatus("Session ended. Choose New session to start again.");
        UpdateActions();
    }

    private async Task ClipboardActionAsync(bool paste)
    {
        try
        {
            if (paste)
            {
                await Terminal.PasteAsync();
            }
            else if (!await Terminal.CopyAsync())
            {
                SetStatus("Select terminal text to copy.");
            }
            Terminal.Focus();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void UpdateActions()
    {
        NewSession.IsEnabled = !starting && !Terminal.IsLive;
        EndSession.IsEnabled = !starting && Terminal.IsLive;
        layout.FindControl<Button>("PasteButton")!.IsEnabled = Terminal.IsLive;
    }

    private void SetStatus(string text, bool error = false)
    {
        Status.Text = text;
        Status.Classes.Set("error", error);
    }
}
