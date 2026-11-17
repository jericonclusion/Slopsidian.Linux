using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RonSijm.Slopsidian.Linux;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class AssistantWindowTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "slopsidian-assistant-window-" + Guid.NewGuid().ToString("N")
    );
    private readonly NativeWorkspace workspace = new();
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public AssistantWindowTests() => Directory.CreateDirectory(directory);

    [AvaloniaFact]
    public async Task NativeConversationUsesAttachedDraftAndFollowupsAndKeepsRealNotesUntouched()
    {
        await PrepareVaultAsync();
        var executable = WriteExecutable(
            "cat > 'received.txt'\nprintf '# Result\\nUse [[Folder/Evidence.md#The-result]].'\n"
        );
        var saved = "";
        var window = CreateWindow(executable, answer =>
        {
            saved = answer;
            return Task.CompletedTask;
        });
        try
        {
            window.Show();
            await window.AttachActiveAsync();
            await window.AddRelatedAsync(false);
            Assert.Equal(2, window.Attachments.Count);
            Assert.Contains(window.Attachments, attachment => attachment.Path == "Folder/Plan.md" && attachment.Draft);
            var view = (NativeAssistantLayout)window.Content!;
            view.FindControl<TextBox>("PromptBox")!.Text = "Use my draft and references";
            await window.SendAsync();
            Assert.Contains("Reply complete", view.FindControl<TextBlock>("AssistantStatus")!.Text);
            Assert.Empty(view.FindControl<TextBox>("PromptBox")!.Text!);
            var save = window
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "SaveReplyButton");
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.StartsWith("# Result", saved);
            view.FindControl<TextBox>("PromptBox")!.Text = "Explain more";
            await window.SendAsync();
            Assert.Contains("Reply complete", view.FindControl<TextBlock>("AssistantStatus")!.Text);
            Assert.Equal(
                "Saved",
                await File.ReadAllTextAsync(Path.Combine(directory, "Folder", "Plan.md"), Cancellation)
            );
            Assert.Equal("Draft [[Evidence#The result]]", workspace.Text);
            Assert.False(File.Exists(Path.Combine(directory, "received.txt")));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CitationNavigationUsesExactPathsAndCurrentHeadingAndBlockOffsets()
    {
        await PrepareVaultAsync();
        var opened = new List<(string Path, int? Offset)>();
        var window = new NativeAssistantWindow(
            workspace,
            new(),
            () => throw new InvalidOperationException(),
            async (path, offset) =>
            {
                opened.Add((path, offset));
                await workspace.OpenTabAsync(path);
            },
            _ => Task.CompletedTask,
            _ => Task.CompletedTask
        );
        try
        {
            window.Show();
            await window.OpenCitationAsync("Folder/Evidence.md#The-result", false, []);
            Assert.Equal("Folder/Evidence.md", opened.Last().Path);
            Assert.Equal(6, opened.Last().Offset);
            await window.OpenCitationAsync("Folder/Evidence.md#^proof", false, []);
            Assert.Equal(21, opened.Last().Offset);
            var count = opened.Count;
            await window.OpenCitationAsync("https://example.com/notes.md", true, []);
            Assert.Equal(count, opened.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task StopAndCloseCancelRequestsAndLeaveComposerAvailableForRetry()
    {
        await PrepareVaultAsync();
        var executable = WriteExecutable("cat >/dev/null\nsleep 30\n");
        var window = CreateWindow(executable);
        try
        {
            window.Show();
            var view = (NativeAssistantLayout)window.Content!;
            view.FindControl<TextBox>("PromptBox")!.Text = "Keep this request";
            var pending = window.SendAsync();
            Assert.True(window.IsRunning);
            var duplicate = window.SendAsync();
            await duplicate;
            view.FindControl<Button>("StopButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pending.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            Assert.False(window.IsRunning);
            Assert.Equal("Keep this request", view.FindControl<TextBox>("PromptBox")!.Text);
            Assert.Contains("stopped", view.FindControl<TextBlock>("AssistantStatus")!.Text);
            var second = window.SendAsync();
            window.Close();
            await second.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            Assert.False(window.IsRunning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ProviderSwitchStartsANewConversationAndRetainsIndependentSettings()
    {
        await PrepareVaultAsync();
        var settings = new NativeAssistantSettings
        {
            Codex = new("/tmp/codex path", "codex-model"),
            Claude = new("/tmp/claude path", "sonnet"),
        };
        var persisted = settings;
        var window = new NativeAssistantWindow(
            workspace,
            settings,
            () => throw new InvalidOperationException(),
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask,
            value =>
            {
                persisted = value;
                return Task.CompletedTask;
            }
        );
        try
        {
            window.Show();
            await window.AttachActiveAsync();
            var view = (NativeAssistantLayout)window.Content!;
            view.FindControl<ComboBox>("ProviderBox")!.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("/tmp/claude path", view.FindControl<TextBox>("ExecutableBox")!.Text);
            Assert.Equal("sonnet", view.FindControl<TextBox>("ModelBox")!.Text);
            Assert.Equal(NativeAssistantProvider.Claude, persisted.Provider);
            Assert.Single(window.Attachments);
            Assert.Contains("New conversation", view.FindControl<TextBlock>("AssistantStatus")!.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task NarrowWindowRetainsSendAndSettingsControlsWithLongErrors()
    {
        await PrepareVaultAsync();
        var window = CreateWindow("/missing-cli");
        try
        {
            window.Width = 560;
            window.Height = 680;
            window.Show();
            await window.AttachActiveAsync();
            await window.AddRelatedAsync(false);
            var view = (NativeAssistantLayout)window.Content!;
            view.FindControl<TextBlock>("AssistantStatus")!.Text = string.Join(
                " ",
                Enumerable.Repeat("A long recoverable provider error.", 60)
            );
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var send = view.FindControl<Button>("SendButton")!;
            Assert.True(view.FindControl<ScrollViewer>("ConversationScroll")!.Bounds.Height >= 119);
            var bottom = send.TranslatePoint(new Avalonia.Point(0, send.Bounds.Height), window)!.Value.Y;
            Assert.True(
                bottom <= window.ClientSize.Height,
                $"Send bottom {bottom} exceeds window height {window.ClientSize.Height}."
            );
            view.FindControl<ToggleButton>("CliSettingsButton")!.IsChecked = true;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FindControl<ScrollViewer>("ConversationScroll")!.Bounds.Height >= 119);
            Assert.False(view.FindControl<WrapPanel>("AttachmentActions")!.IsVisible);
            Assert.Equal(2, window.Attachments.Count);
            bottom = send.TranslatePoint(new Avalonia.Point(0, send.Bounds.Height), window)!.Value.Y;
            Assert.True(bottom <= window.ClientSize.Height, $"Settings pushed Send to {bottom}.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CompactContextCanBeInspectedAndRemovedAndSettingsKeepComposerDrafts()
    {
        await PrepareVaultAsync();
        var window = CreateWindow("/missing-cli");
        window.Show();
        try
        {
            await window.AttachActiveAsync();
            var view = (NativeAssistantLayout)window.Content!;
            var prompt = view.FindControl<TextBox>("PromptBox")!;
            var toggle = view.FindControl<ToggleButton>("CliSettingsButton")!;
            Assert.False(view.FindControl<Button>("SendButton")!.IsEnabled);
            prompt.Text = "Keep my question";
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.FindControl<Button>("SendButton")!.IsEnabled);
            toggle.IsChecked = true;
            view.FindControl<TextBox>("ExecutableBox")!.Text = "/custom/cli";
            view.FindControl<TextBox>("ModelBox")!.Text = "chosen-model";
            toggle.IsChecked = false;
            toggle.IsChecked = true;
            Assert.Equal("Keep my question", prompt.Text);
            Assert.Equal("/custom/cli", view.FindControl<TextBox>("ExecutableBox")!.Text);
            Assert.Equal("chosen-model", view.FindControl<TextBox>("ModelBox")!.Text);
            Assert.Single(window.Attachments);
            toggle.IsChecked = false;
            window.UpdateLayout();
            var attachment = Assert.Single(window.Attachments);
            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
            var inspect = buttons.Single(button =>
                AutomationProperties.GetName(button) == "Inspect attached " + attachment.Label
            );
            inspect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var snapshot = Assert.Single(window.OwnedWindows);
            Assert.Equal(attachment.Content, Assert.IsType<TextBox>(snapshot.Content).Text);
            snapshot.Close();
            buttons
                .Single(button => AutomationProperties.GetName(button) == "Remove attached " + attachment.Label)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(window.Attachments);
            Assert.False(view.FindControl<ScrollViewer>("ContextScroll")!.IsVisible);
            Assert.Equal("Keep my question", prompt.Text);
            Assert.Equal("Draft [[Evidence#The result]]", workspace.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ClosedOrStaleVaultWindowCannotAttachOrSendToAnotherVault()
    {
        await PrepareVaultAsync();
        var window = CreateWindow("/missing-cli");
        try
        {
            window.Show();
            await window.AttachActiveAsync();
            workspace.DiscardEdits();
            await workspace.OpenVaultAsync(directory);
            var view = (NativeAssistantLayout)window.Content!;
            view.FindControl<TextBox>("PromptBox")!.Text = "Do not send";
            await window.SendAsync();
            Assert.Contains("vault changed", view.FindControl<TextBlock>("AssistantStatus")!.Text);
            Assert.False(window.IsRunning);
        }
        finally
        {
            window.Close();
        }
    }

    private async Task PrepareVaultAsync()
    {
        Directory.CreateDirectory(Path.Combine(directory, "Folder"));
        await File.WriteAllTextAsync(Path.Combine(directory, "Folder", "Plan.md"), "Saved", Cancellation);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "Folder", "Evidence.md"),
            "Intro\n## The result\n\nEvidence ^proof\n",
            Cancellation
        );
        await File.WriteAllTextAsync(Path.Combine(directory, "Evidence.md"), "Wrong root note", Cancellation);
        await workspace.OpenVaultAsync(directory);
        await workspace.OpenTabAsync("Folder/Plan.md");
        workspace.Text = "Draft [[Evidence#The result]]";
        await workspace.IndexAsync(Cancellation);
    }

    private NativeAssistantWindow CreateWindow(string executable, Func<string, Task>? save = null) =>
        new(
            workspace,
            new NativeAssistantSettings { Codex = new(executable) },
            () => workspace.CaptureAssistantSelection("Folder/Plan.md", 0, 5),
            (_, _) => Task.CompletedTask,
            save ?? (_ => Task.CompletedTask),
            _ => Task.CompletedTask
        );

    private string WriteExecutable(string body)
    {
        var path = Path.Combine(directory, "fake cli");
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }

    public void Dispose()
    {
        workspace.Dispose();
        Directory.Delete(directory, true);
    }
}
