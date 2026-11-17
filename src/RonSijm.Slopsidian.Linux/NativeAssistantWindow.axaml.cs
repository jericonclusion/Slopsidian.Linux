using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeAssistantWindow : Window
{
    private readonly NativeAssistantLayout layout = new();
    private ComboBox ProviderBox => layout.FindControl<ComboBox>("ProviderBox")!;
    private TextBox ExecutableBox => layout.FindControl<TextBox>("ExecutableBox")!;
    private TextBox ModelBox => layout.FindControl<TextBox>("ModelBox")!;
    private TextBox PromptBox => layout.FindControl<TextBox>("PromptBox")!;
    private TextBlock AssistantStatus => layout.FindControl<TextBlock>("AssistantStatus")!;
    private TextBlock ContextStatus => layout.FindControl<TextBlock>("ContextStatus")!;
    private Border CliSettings => layout.FindControl<Border>("CliSettings")!;
    private ToggleButton CliSettingsButton => layout.FindControl<ToggleButton>("CliSettingsButton")!;
    private WrapPanel AttachmentActions => layout.FindControl<WrapPanel>("AttachmentActions")!;
    private StackPanel AttachmentList => layout.FindControl<StackPanel>("AttachmentList")!;
    private StackPanel ConversationContent => layout.FindControl<StackPanel>("ConversationContent")!;
    private StackPanel ConversationEmpty => layout.FindControl<StackPanel>("ConversationEmpty")!;
    private ScrollViewer ConversationScroll => layout.FindControl<ScrollViewer>("ConversationScroll")!;
    private Button NewConversationButton => layout.FindControl<Button>("NewConversationButton")!;
    private Button PreviewMessageButton => layout.FindControl<Button>("PreviewMessageButton")!;
    private Button StopButton => layout.FindControl<Button>("StopButton")!;
    private Button SendButton => layout.FindControl<Button>("SendButton")!;
    private readonly NativeWorkspace workspace;
    private readonly string root;
    private readonly long epoch;
    private readonly Func<NativeAssistantAttachment> selection;
    private readonly Func<string, int?, Task> openNote;
    private readonly Func<string, Task> saveAnswer;
    private readonly Func<NativeAssistantSettings, Task> persist;
    private readonly List<NativeAssistantAttachment> attachments = [];
    private readonly List<NativeAssistantTurn> history = [];
    private readonly List<(StackPanel Body, string Answer, NativeAssistantAttachment[] Context)> renderedReplies = [];
    private readonly App? appearanceApp;
    private NativeAssistantSettings settings;
    private CancellationTokenSource? request;
    private bool changingProvider;
    private bool closed;
    private bool panelBudgetQueued;

    public NativeAssistantWindow(
        NativeWorkspace workspace,
        NativeAssistantSettings settings,
        Func<NativeAssistantAttachment> selection,
        Func<string, int?, Task> openNote,
        Func<string, Task> saveAnswer,
        Func<NativeAssistantSettings, Task> persist
    )
    {
        this.workspace = workspace;
        this.settings = settings;
        this.selection = selection;
        this.openNote = openNote;
        this.saveAnswer = saveAnswer;
        this.persist = persist;
        root = workspace.Snapshot.Root
            ?? throw new NativeActionRequiredException("Open a vault first.", NativeErrorAction.OpenVault);
        epoch = workspace.VaultEpoch;
        Title = "Assistant — Slopsidian Linux";
        Width = 860;
        Height = 860;
        MinWidth = 560;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = layout;
        layout.LayoutUpdated += (_, _) => QueuePanelBudget();
        NewConversationButton.Click += NewConversation;
        ProviderBox.SelectionChanged += ProviderChanged;
        layout.FindControl<Button>("SaveCliSettingsButton")!.Click += SaveCliSettings;
        layout.FindControl<Button>("AttachActiveButton")!.Click += AttachActive;
        layout.FindControl<Button>("AttachSelectionButton")!.Click += AttachSelection;
        layout.FindControl<Button>("AttachOtherButton")!.Click += AttachOther;
        layout.FindControl<Button>("AttachLinkedButton")!.Click += AttachLinked;
        layout.FindControl<Button>("AttachBacklinksButton")!.Click += AttachBacklinks;
        PreviewMessageButton.Click += PreviewMessage;
        SendButton.Click += SendRequest;
        StopButton.Click += StopRequest;
        PromptBox.TextChanged += (_, _) => UpdateComposerActions();
        changingProvider = true;
        ProviderBox.SelectedIndex = (int)settings.Provider;
        LoadProvider();
        changingProvider = false;
        KeyDown += HandleKey;
        appearanceApp = Application.Current as App;
        if (appearanceApp is not null)
        {
            appearanceApp.AppearanceChanged += RefreshReplyAppearance;
        }
        Closed += (_, _) =>
        {
            closed = true;
            request?.Cancel();
            if (appearanceApp is not null)
            {
                appearanceApp.AppearanceChanged -= RefreshReplyAppearance;
            }
        };
    }

    public IReadOnlyList<NativeAssistantAttachment> Attachments => attachments;
    public bool IsRunning => request is not null;

    private void QueuePanelBudget()
    {
        if (closed || panelBudgetQueued)
        {
            return;
        }
        panelBudgetQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                panelBudgetQueued = false;
                if (closed)
                {
                    return;
                }
                var grid = layout.FindControl<Grid>("AssistantRoot")!;
                PromptBox.MaxHeight = ClientSize.Height < 760 ? 84 : 132;
                var detailsVisible = !CliSettings.IsVisible || ClientSize.Height >= 800;
                AttachmentActions.IsVisible = detailsVisible;
                var context = layout.FindControl<ScrollViewer>("ContextScroll")!;
                context.IsVisible = detailsVisible && attachments.Count > 0;
                var scrolling = CliSettings.IsVisible
                    ? layout.FindControl<ScrollViewer>("CliSettingsScroll")!
                    : context;
                var occupied =
                    grid.RowDefinitions[0].ActualHeight
                    + grid.RowDefinitions[1].ActualHeight
                    + grid.RowDefinitions[3].ActualHeight;
                // Measure the actual native font/control sizes before reserving a readable transcript.
                var maximum = Math.Clamp(
                    scrolling.Bounds.Height + grid.Bounds.Height - occupied - 120,
                    0,
                    CliSettings.IsVisible ? 180 : 88
                );
                if (Math.Abs(scrolling.MaxHeight - maximum) > 1)
                {
                    scrolling.MaxHeight = maximum;
                }
            },
            DispatcherPriority.Background
        );
    }

    public Task AttachInitialAsync() => GuardAsync(AttachActiveAsync);

    public async Task AttachActiveAsync()
    {
        RequireVault();
        var path = workspace.ActiveTab?.Path ?? throw new InvalidDataException("Open a Markdown note first.");
        AddAttachments([await workspace.CaptureAssistantNoteAsync(path, "Active note", CancellationToken.None)]);
    }

    private void RequireVault()
    {
        if (closed)
        {
            throw new InvalidOperationException("This assistant window is closed.");
        }
        workspace.RequireAssistantEpoch(root, epoch);
    }

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            RequireVault();
            await action();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!closed)
            {
                AssistantStatus.Text = exception.Message;
                AssistantStatus.Classes.Add("error");
            }
        }
    }

    private void SetStatus(string text)
    {
        AssistantStatus.Classes.Remove("error");
        AssistantStatus.Text = text;
    }

    private void UpdateComposerActions()
    {
        var available = !IsRunning && !string.IsNullOrWhiteSpace(PromptBox.Text);
        SendButton.IsEnabled = available;
        PreviewMessageButton.IsEnabled = available;
    }

    private NativeAssistantProvider Provider => (NativeAssistantProvider)ProviderBox.SelectedIndex;

    private void LoadProvider()
    {
        var value = settings.For(Provider);
        ExecutableBox.Text = value.Executable;
        ModelBox.Text = value.Model;
    }

    private async void ProviderChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (changingProvider || ExecutableBox is null || ProviderBox.SelectedIndex < 0)
        {
            return;
        }
        await GuardAsync(async () =>
        {
            // Keep drafts of each provider's settings, but do not send one provider's chat to another.
            settings = settings.With(settings.Provider, new(ExecutableBox.Text ?? "", ModelBox.Text ?? ""));
            settings = settings with { Provider = Provider };
            LoadProvider();
            ResetConversation();
            await PersistSettingsAsync();
        });
    }

    private async Task PersistSettingsAsync()
    {
        settings = settings.With(Provider, new(ExecutableBox.Text?.Trim() ?? "", ModelBox.Text?.Trim() ?? ""));
        NativeAssistantSettings.Validate(settings);
        await persist(settings);
    }

    private async void SaveCliSettings(object? sender, RoutedEventArgs args) =>
        await GuardAsync(async () =>
        {
            await PersistSettingsAsync();
            SetStatus("CLI settings saved. Your login remains managed by the CLI.");
        });

    private void NewConversation(object? sender, RoutedEventArgs args) => ResetConversation();

    private void ResetConversation()
    {
        history.Clear();
        renderedReplies.Clear();
        ConversationContent.Children.Clear();
        ConversationContent.Children.Add(ConversationEmpty);
        ConversationEmpty.IsVisible = true;
        SetStatus("New conversation. Attached snapshots are retained; reattach a note to refresh it.");
    }

    private async void PreviewMessage(object? sender, RoutedEventArgs args) =>
        await GuardAsync(() =>
        {
            ShowText(
                "Message to " + ProviderName(Provider),
                NativeAssistantContext.Build(PromptBox.Text ?? "", attachments, history)
            );
            return Task.CompletedTask;
        });

    private void ShowText(string title, string text)
    {
        var view = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("DejaVu Sans Mono, monospace"),
            FontSize = 13,
            Margin = new Thickness(16),
        };
        new Window
        {
            Title = title,
            Width = 760,
            Height = 650,
            MinWidth = 420,
            MinHeight = 300,
            Content = view,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        }.Show(this);
    }

    private async void HandleKey(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && args.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            args.Handled = true;
            await SendAsync();
        }
        else if (args.Key == Key.Escape && request is not null)
        {
            args.Handled = true;
            await request.CancelAsync();
        }
    }
}
