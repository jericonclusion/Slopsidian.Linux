using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeAssistantWindow
{
    private async void SendRequest(object? sender, RoutedEventArgs args) => await SendAsync();

    private async void StopRequest(object? sender, RoutedEventArgs args)
    {
        if (request is not null)
        {
            await request.CancelAsync();
        }
    }

    public Task SendAsync() =>
        GuardAsync(async () =>
        {
            if (request is not null)
            {
                return;
            }
            var prompt = PromptBox.Text ?? "";
            var context = attachments.ToArray();
            var message = NativeAssistantContext.Build(prompt, context, history);
            var provider = Provider;
            using var cancellation = new CancellationTokenSource();
            request = cancellation;
            SetRunning(true);
            StackPanel? reply = null;
            try
            {
                await PersistSettingsAsync();
                RequireVault();
                var providerSettings = settings.For(provider);
                ConversationEmpty.IsVisible = false;
                AddQuestion(prompt, context);
                reply = new StackPanel { Spacing = 12 };
                reply.Children.Add(new TextBlock { Text = ProviderName(provider), FontWeight = FontWeight.SemiBold });
                var body = new StackPanel { Spacing = 12 };
                var streaming = new SelectableTextBlock { Text = "", TextWrapping = TextWrapping.Wrap };
                body.Children.Add(streaming);
                reply.Children.Add(body);
                ConversationContent.Children.Add(reply);
                SetStatus("Waiting for " + ProviderName(provider) + "… You can stop this request.");
                ConversationScroll.ScrollToEnd();
                var answer = await NativeAssistantCli.RunAsync(
                    provider,
                    providerSettings,
                    message,
                    chunk => AppendOutput(streaming, chunk, cancellation),
                    cancellationToken: cancellation.Token
                );
                RequireVault();
                cancellation.Token.ThrowIfCancellationRequested();
                history.Add(new NativeAssistantTurn(prompt, answer));
                NativeMarkdown.Render(
                    body,
                    answer,
                    link => _ = OpenCitationAsync(link, false, context),
                    link => _ = OpenCitationAsync(link, true, context)
                );
                AddReplyActions(reply, answer, context);
                PromptBox.Text = "";
                SetStatus("Reply complete. Copy it or save it as a new note.");
            }
            catch (OperationCanceledException)
            {
                MarkIncompleteReply(reply);
                if (!closed)
                {
                    SetStatus(
                        "Request stopped. Your request is still in the composer; partial output is not added to the conversation context."
                    );
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                MarkIncompleteReply(reply);
                throw;
            }
            finally
            {
                request = null;
                if (!closed)
                {
                    SetRunning(false);
                }
            }
        });

    private static void MarkIncompleteReply(StackPanel? reply)
    {
        reply?.Children.Add(
            new TextBlock
            {
                Text = "Incomplete reply · excluded from follow-up context.",
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap,
            }
        );
    }

    private void AppendOutput(SelectableTextBlock target, string chunk, CancellationTokenSource cancellation) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!closed && request == cancellation && !cancellation.IsCancellationRequested)
            {
                target.Text += chunk;
            }
        });

    private void AddQuestion(string prompt, NativeAssistantAttachment[] context)
    {
        var question = new StackPanel { Spacing = 6 };
        question.Children.Add(new TextBlock { Text = "You", FontWeight = FontWeight.SemiBold });
        question.Children.Add(new SelectableTextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        if (context.Length > 0)
        {
            question.Children.Add(
                new TextBlock
                {
                    Text = "Context: " + string.Join(", ", context.Select(item => item.Label)),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Classes = { "muted" },
                }
            );
        }
        ConversationContent.Children.Add(question);
    }

    private void SetRunning(bool running)
    {
        ProviderBox.IsEnabled = !running;
        CliSettings.IsEnabled = !running;
        PromptBox.IsEnabled = !running;
        AttachmentActions.IsEnabled = !running;
        AttachmentList.IsEnabled = !running;
        NewConversationButton.IsEnabled = !running;
        PreviewMessageButton.IsEnabled = !running;
        SendButton.IsEnabled = !running;
        StopButton.IsVisible = running;
    }

    private void AddReplyActions(StackPanel reply, string answer, NativeAssistantAttachment[] context)
    {
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        var copy = new Button { Content = "Copy reply", Margin = new Thickness(0, 0, 8, 6) };
        copy.Click += async (_, _) =>
            await GuardAsync(async () =>
            {
                if (Clipboard is null)
                {
                    throw new InvalidOperationException("Clipboard access is unavailable.");
                }
                await Clipboard.SetTextAsync(answer);
                SetStatus("Reply copied.");
            });
        actions.Children.Add(copy);
        var save = new Button
        {
            Content = "Save as new note…",
            Foreground = NativePalette.Brush("AssistantActionBrush"),
            Margin = new Thickness(0, 0, 8, 6),
        };
        save.Click += async (_, _) =>
            await GuardAsync(async () =>
            {
                await saveAnswer(answer);
            });
        actions.Children.Add(save);
        reply.Children.Add(actions);
        if (context.Length == 0)
        {
            return;
        }
        var sources = new WrapPanel();
        foreach (var path in context.Select(item => item.Path).Distinct(StringComparer.Ordinal))
        {
            var source = new Button
            {
                Content = new TextBlock
                {
                    Text = path,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                },
                Margin = new Thickness(0, 0, 6, 6),
            };
            source.Click += async (_, _) => await GuardAsync(() => openNote(path, null));
            sources.Children.Add(source);
        }
        reply.Children.Add(
            new TextBlock
            {
                Text = "Attached sources",
                FontSize = 12,
                Classes = { "muted" },
            }
        );
        reply.Children.Add(sources);
    }

    public Task OpenCitationAsync(string target, bool markdown, NativeAssistantAttachment[] context) =>
        GuardAsync(async () =>
        {
            var parts = target.Split('#', 2);
            var pathPart = (markdown ? Uri.UnescapeDataString(parts[0]) : parts[0]).TrimStart('/');
            var path = workspace.Snapshot.Files.FirstOrDefault(file => file.Path == pathPart)?.Path
                ?? workspace.ResolveLinkTarget(context.FirstOrDefault()?.Path ?? "", target, markdown)
                ?? throw new InvalidDataException("This citation does not resolve to a file in the current vault.");
            var fragment = parts.ElementAtOrDefault(1);
            if (markdown && fragment is not null)
            {
                fragment = Uri.UnescapeDataString(fragment);
            }
            await openNote(path, null);
            if (fragment is not null)
            {
                RequireVault();
                var text = workspace.Tabs.FirstOrDefault(tab => tab.Path == path)?.Text
                    ?? throw new InvalidDataException("The cited note could not be opened.");
                await openNote(path, NativePreviewMedia.FragmentOffset(text, fragment));
            }
        });

    private static string ProviderName(NativeAssistantProvider provider) =>
        provider switch
        {
            NativeAssistantProvider.Codex => "Codex CLI",
            NativeAssistantProvider.Claude => "Claude Code",
            NativeAssistantProvider.Copilot => "GitHub Copilot CLI",
            _ => "CLI",
        };
}
