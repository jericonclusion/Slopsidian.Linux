using Avalonia.Interactivity;

namespace RonSijm.Slopsidian.Linux;

public partial class MainWindow
{
    private NativeAssistantWindow? assistantWindow;

    private async void OpenAssistant(object? sender, RoutedEventArgs args) => await OpenAssistantAsync();

    public Task OpenAssistantAsync() =>
        RunAsync(async () =>
        {
            if (!profile.State.Preferences.CliAssistantEnabled)
            {
                throw new InvalidOperationException("Enable CLI assistant in Settings → Native modules first.");
            }
            if (assistantWindow is not null)
            {
                assistantWindow.Activate();
                return;
            }
            var root = Workspace.Snapshot.Root ?? throw new InvalidOperationException("Open a vault first.");
            var epoch = Workspace.VaultEpoch;
            var assistant = new NativeAssistantWindow(
                Workspace,
                profile.State.Assistant,
                () =>
                {
                    if (EditorTabs.SelectedIndex != 0 || displayedTab is null)
                    {
                        throw new InvalidDataException("Select text in a Markdown note's Source tab first.");
                    }
                    return Workspace.CaptureAssistantSelection(
                        displayedTab.Path,
                        Editor.SelectionStart,
                        Editor.SelectionLength
                    );
                },
                (path, offset) =>
                {
                    Workspace.RequireAssistantEpoch(root, epoch);
                    return OpenPathAsync(path, offset);
                },
                answer => SaveAssistantAnswerAsync(root, epoch, answer),
                async settings =>
                {
                    Workspace.RequireAssistantEpoch(root, epoch);
                    profile.State = profile.State with { Assistant = settings };
                    await PersistProfileAsync();
                }
            );
            assistantWindow = assistant;
            assistant.Closed += (_, _) =>
            {
                if (assistantWindow == assistant)
                {
                    assistantWindow = null;
                }
            };
            assistant.Show(this);
            if (Workspace.ActiveTab?.Document.File.Extension == "md")
            {
                await assistant.AttachInitialAsync();
            }
        });

    private Task SaveAssistantAnswerAsync(string root, long epoch, string answer) =>
        RunAsync(async () =>
        {
            Workspace.RequireAssistantEpoch(root, epoch);
            var path = await AskTextAsync("Save assistant reply", "New note path", "Assistant reply.md", "Create note");
            if (path is null)
            {
                return;
            }
            Workspace.RequireAssistantEpoch(root, epoch);
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                path += ".md";
            }
            await Workspace.CreateTabAsync(path, answer);
            FileTree.ItemsSource = NativeFileNode.FromSnapshot(Workspace.Snapshot);
            LoadEditor();
        });
}
