using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeAssistantWindow
{
    private void AddAttachments(IEnumerable<NativeAssistantAttachment> incoming)
    {
        RequireVault();
        var next = attachments.ToList();
        foreach (var item in incoming)
        {
            next.RemoveAll(previous =>
                previous.Path == item.Path && previous.StartLine == item.StartLine && previous.EndLine == item.EndLine
            );
            next.Add(item);
        }
        NativeAssistantContext.ValidateAttachments(next);
        attachments.Clear();
        attachments.AddRange(next);
        RenderAttachments();
    }

    private void RenderAttachments()
    {
        AttachmentList.Children.Clear();
        foreach (var attachment in attachments.ToArray())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
            var title = new StackPanel { Spacing = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            title.Children.Add(
                new TextBlock { Text = attachment.Label, TextTrimming = TextTrimming.CharacterEllipsis }
            );
            title.Children.Add(
                new TextBlock
                {
                    Text = attachment.Description,
                    FontSize = 12,
                    Classes = { "muted" },
                }
            );
            ToolTip.SetTip(title, attachment.Label);
            row.Children.Add(title);
            var inspect = new Button { Content = "Inspect", Padding = new Thickness(8, 4) };
            AutomationProperties.SetName(inspect, "Inspect attached " + attachment.Label);
            inspect.Click += (_, _) => ShowText(attachment.Label + " — attached snapshot", attachment.Content);
            Grid.SetColumn(inspect, 1);
            row.Children.Add(inspect);
            var remove = new Button
            {
                Content = new NativeIcon { Kind = "close" },
                Classes = { "icon" },
            };
            AutomationProperties.SetName(remove, "Remove attached " + attachment.Label);
            ToolTip.SetTip(remove, "Remove attachment");
            remove.Click += (_, _) =>
            {
                attachments.Remove(attachment);
                RenderAttachments();
            };
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
            AttachmentList.Children.Add(row);
        }
        ContextStatus.Text =
            attachments.Count == 0
                ? "No notes attached."
                : $"{attachments.Count} snapshots · {attachments.Sum(item => item.Content.Length):N0} characters. Reattach to refresh edited text.";
    }

    private async void AttachActive(object? sender, RoutedEventArgs args) => await GuardAsync(AttachActiveAsync);

    private async void AttachSelection(object? sender, RoutedEventArgs args) =>
        await GuardAsync(() =>
        {
            AddAttachments([selection()]);
            return Task.CompletedTask;
        });

    private async void AttachOther(object? sender, RoutedEventArgs args) =>
        await GuardAsync(async () =>
        {
            var choices = workspace.Notes.Select(note => new NativeChoice(note.Basename, note.Path, note.Path));
            var choice = await new NativePicker("Attach a note", choices).ShowDialog<NativeChoice?>(this);
            RequireVault();
            if (choice is not null)
            {
                AddAttachments([
                    await workspace.CaptureAssistantNoteAsync(choice.Value, cancellationToken: CancellationToken.None),
                ]);
            }
        });

    private async void AttachLinked(object? sender, RoutedEventArgs args) => await AddRelatedAsync(false);

    private async void AttachBacklinks(object? sender, RoutedEventArgs args) => await AddRelatedAsync(true);

    public Task AddRelatedAsync(bool backlinks) =>
        GuardAsync(async () =>
        {
            var active = workspace.ActiveTab?.Path ?? throw new InvalidDataException("Open a Markdown note first.");
            var paths = backlinks
                ? workspace.Backlinks(active).Select(link => link.Path)
                : workspace
                    .MetadataFor(active)
                    .Links.Select(link => workspace.ResolveLinkTarget(active, link.Target, link.Markdown));
            var targets = paths
                .Where(path => path is not null && path != active)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .Where(path => workspace.Notes.Any(note => note.Path == path))
                .Take(NativeAssistantContext.MaximumAttachments + 1)
                .ToArray();
            if (targets.Length == 0)
            {
                SetStatus(
                    backlinks
                        ? "This note has no resolved backlinks."
                        : "This note has no resolved links to Markdown notes."
                );
                return;
            }
            var next = new List<NativeAssistantAttachment>();
            foreach (var path in targets)
            {
                next.Add(
                    await workspace.CaptureAssistantNoteAsync(
                        path,
                        backlinks ? "Backlink to " + active : "Linked from " + active,
                        CancellationToken.None
                    )
                );
            }
            AddAttachments(next);
            SetStatus($"Added {targets.Length} related note snapshots. Inspect or remove any before sending.");
        });
}
