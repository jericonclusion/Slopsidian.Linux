using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace RonSijm.Slopsidian.Linux;

public sealed class NativeFilePreview : UserControl, IDisposable
{
    private readonly NativeWorkspace workspace;
    private readonly string path;
    private readonly string subpath;
    private readonly string root;
    private readonly long epoch;
    private readonly Action<string, string, bool> navigate;
    private readonly NativePreviewBudget budget;
    private readonly string? backgroundStyle;
    private readonly ContentControl body = new();
    private readonly TextBlock message = new()
    {
        Text = "Loading preview…",
        TextWrapping = TextWrapping.Wrap,
        Classes = { "muted" },
    };
    private readonly Button retry = new()
    {
        Content = "Reload preview",
        IsVisible = false,
        HorizontalAlignment = HorizontalAlignment.Left,
    };
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? loading;
    private NativeTab? draft;
    private IDisposable? contentLifetime;
    private int generation;
    private long revision;
    private bool attached;
    private bool retained;
    private bool waiting;

    public NativeFilePreview(
        NativeWorkspace workspace,
        string path,
        string subpath,
        Action<string, string, bool> navigate
    )
        : this(workspace, path, subpath, navigate, new NativePreviewBudget(), null) { }

    internal NativeFilePreview(
        NativeWorkspace workspace,
        string path,
        string subpath,
        Action<string, string, bool> navigate,
        NativePreviewBudget budget,
        string? backgroundStyle = null
    )
    {
        this.workspace = workspace;
        this.path = path;
        this.subpath = subpath;
        this.navigate = navigate;
        this.budget = budget;
        this.backgroundStyle = backgroundStyle;
        root = workspace.Snapshot.Root ?? throw new InvalidOperationException("Open a vault before previewing a file.");
        epoch = workspace.VaultEpoch;
        AutomationProperties.SetName(this, "Preview of " + path + subpath);
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 8 };
        layout.Children.Add(body);
        layout.Children.Add(message);
        Grid.SetRow(retry, 1);
        layout.Children.Add(retry);
        Content = layout;
        retry.Click += (_, _) => Reload();
        poll.Tick += (_, _) =>
        {
            if (waiting || revision != workspace.Revision)
            {
                Reload();
            }
        };
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => Detach();
    }

    public Task Loading { get; private set; } = Task.CompletedTask;
    public bool HasLoadedContent { get; private set; }
    public bool IsDisposed { get; private set; }
    public string? Failure { get; private set; }

    private bool RootIsCurrent() => workspace.Snapshot.Root == root && workspace.VaultEpoch == epoch;

    private bool Current(int request) => !IsDisposed && attached && generation == request && RootIsCurrent();

    private void Attach()
    {
        if (attached || IsDisposed)
        {
            return;
        }
        attached = true;
        ((INotifyCollectionChanged)workspace.Tabs).CollectionChanged += TabsChanged;
        BindDraft();
        poll.Start();
        Reload();
    }

    private void Detach()
    {
        if (!attached)
        {
            return;
        }
        attached = false;
        poll.Stop();
        ((INotifyCollectionChanged)workspace.Tabs).CollectionChanged -= TabsChanged;
        if (draft is not null)
        {
            draft.PropertyChanged -= DraftChanged;
            draft = null;
        }
        CancelLoading();
        ClearContent();
        Release();
    }

    private bool BindDraft()
    {
        var next = workspace.Tabs.FirstOrDefault(tab => tab.Path == path);
        if (ReferenceEquals(next, draft))
        {
            return false;
        }
        if (draft is not null)
        {
            draft.PropertyChanged -= DraftChanged;
        }
        draft = next;
        if (draft is not null)
        {
            draft.PropertyChanged += DraftChanged;
        }
        return true;
    }

    private void TabsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (BindDraft() || !RootIsCurrent())
        {
            Reload();
        }
    }

    private void DraftChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(NativeTab.Text))
        {
            Reload();
        }
    }

    public void Reload()
    {
        if (!attached || IsDisposed)
        {
            return;
        }
        CancelLoading();
        ClearContent();
        revision = workspace.Revision;
        if (!RootIsCurrent())
        {
            Failed("This preview belongs to a previous vault.");
            return;
        }
        if (!retained)
        {
            retained = budget.Acquire();
        }
        waiting = !retained;
        if (waiting)
        {
            ShowMessage("Zoom in to load more file previews.");
            return;
        }
        Failure = null;
        retry.IsVisible = false;
        ShowMessage("Loading preview…");
        loading = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var capturedText = draft?.Text;
        Loading = LoadAsync(generation, capturedText, loading.Token);
    }

    private async Task LoadAsync(int request, string? capturedText, CancellationToken cancellationToken)
    {
        try
        {
            await NativePreviewBudget.LoadSlots.WaitAsync(cancellationToken);
            LoadedPreview loaded;
            try
            {
                loaded = await Task.Run(() => ReadAsync(capturedText, cancellationToken), cancellationToken);
            }
            finally
            {
                NativePreviewBudget.LoadSlots.Release();
            }
            using (loaded)
            {
                if (!Current(request))
                {
                    return;
                }
                if (loaded.Raster is { } raster)
                {
                    body.Content = RasterControl(raster);
                    contentLifetime = raster;
                    loaded.Raster = null;
                }
                else
                {
                    RenderText(loaded.Text ?? "");
                }
                message.IsVisible = false;
                HasLoadedContent = true;
            }
        }
        catch (OperationCanceledException)
        {
            if (Current(request))
            {
                Failed("Preview timed out. Reload to try again.");
            }
        }
        catch (Exception exception)
        {
            if (Current(request))
            {
                Failed(exception.Message);
            }
        }
    }

    private async Task<LoadedPreview> ReadAsync(string? capturedText, CancellationToken cancellationToken)
    {
        var extension = System.IO.Path.GetExtension(path).TrimStart('.');
        var raster = NativePreviewMedia.IsRaster(extension);
        if (backgroundStyle is not null && !raster)
        {
            throw new InvalidDataException("Group backgrounds require a local raster image.");
        }
        if (!raster && !NativePreviewMedia.IsText(extension))
        {
            throw new InvalidDataException("This file type does not have a native preview yet.");
        }
        if (!raster && capturedText is not null)
        {
            return new LoadedPreview(capturedText);
        }
        var file = await workspace.Vault.ReadPreviewAsync(
            path,
            root,
            raster ? 16 * 1024 * 1024 : 4 * 1024 * 1024,
            cancellationToken
        );
        return raster
            ? new LoadedPreview(NativePreviewMedia.DecodeRaster(file.Bytes, cancellationToken))
            : new LoadedPreview(NativePreviewMedia.DecodeText(file.Bytes));
    }

    private Control RasterControl(NativeDecodedRaster raster)
    {
        if (backgroundStyle == "repeat")
        {
            return new Border
            {
                Background = new ImageBrush(raster.Bitmap)
                {
                    Stretch = Stretch.None,
                    TileMode = TileMode.Tile,
                    DestinationRect = new RelativeRect(
                        0,
                        0,
                        raster.OriginalSize.Width,
                        raster.OriginalSize.Height,
                        RelativeUnit.Absolute
                    ),
                },
            };
        }
        var stretch = backgroundStyle switch
        {
            null => Stretch.Uniform,
            "ratio" => Stretch.Uniform,
            _ => Stretch.UniformToFill,
        };
        var image = new Image { Source = raster.Bitmap, Stretch = stretch };
        AutomationProperties.SetName(image, path);
        return image;
    }

    private void RenderText(string text)
    {
        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (text.Length > 1_000_000)
        {
            throw new InvalidDataException("This note is too large for the native preview. Open Source to read it.");
        }
        if (extension == ".canvas")
        {
            RenderCanvas(text);
            return;
        }
        var content = new StackPanel { Spacing = 10 };
        if (extension is ".md" or ".markdown")
        {
            NativeMarkdown.Render(
                content,
                NativePreviewMedia.Section(text, subpath),
                link => Navigate(link, false),
                link => Navigate(link, true)
            );
        }
        else
        {
            content.Children.Add(new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
        }
        body.Content = content;
    }

    private void RenderCanvas(string text)
    {
        var model = new NativeCanvasDocument(text);
        var cards = model.GetCards();
        var visible = cards.Take(200).ToArray();
        var ids = visible.Select(card => card.Id).ToHashSet(StringComparer.Ordinal);
        var canvas = new NativeCanvasSurface(ThumbnailCard)
        {
            Height = 220,
            Focusable = false,
            IsHitTestVisible = false,
        };
        contentLifetime = canvas;
        canvas.SetData(
            visible,
            model.GetConnections().Where(edge => ids.Contains(edge.From) && ids.Contains(edge.To)).ToArray()
        );
        var content = new StackPanel { Spacing = 8, Children = { canvas } };
        if (cards.Count > visible.Length)
        {
            content.Children.Add(
                new TextBlock
                {
                    Text = "Preview includes the first 200 cards. Open this Canvas to view all cards.",
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "muted" },
                }
            );
        }
        body.Content = content;
    }

    private StackPanel ThumbnailCard(NativeCanvasCard card)
    {
        var content = new StackPanel { Spacing = 8 };
        if (card.Type == "text")
        {
            NativeMarkdown.Render(content, card.Text, link => Navigate(link, false), link => Navigate(link, true));
        }
        else
        {
            content.Children.Add(
                new TextBlock
                {
                    Text = card.Type == "group" ? card.Text : card.Target,
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }
        return content;
    }

    private void Navigate(string link, bool markdown)
    {
        if (RootIsCurrent() && !IsDisposed)
        {
            navigate(path, link, markdown);
        }
    }

    private void ShowMessage(string text)
    {
        message.Text = text;
        message.IsVisible = true;
    }

    private void Failed(string text)
    {
        ClearContent();
        Failure = text;
        waiting = false;
        retry.IsVisible = true;
        ShowMessage(text);
        Release();
    }

    private void Release()
    {
        if (retained)
        {
            retained = false;
            budget.Release();
        }
    }

    private void ClearContent()
    {
        body.Content = null;
        contentLifetime?.Dispose();
        contentLifetime = null;
        HasLoadedContent = false;
    }

    private void CancelLoading()
    {
        generation++;
        loading?.Cancel();
        loading?.Dispose();
        loading = null;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        IsDisposed = true;
        Detach();
        CancelLoading();
        ClearContent();
        Release();
    }

    private sealed class LoadedPreview : IDisposable
    {
        public LoadedPreview(string text) => Text = text;

        public LoadedPreview(NativeDecodedRaster raster) => Raster = raster;

        public string? Text { get; }
        public NativeDecodedRaster? Raster { get; set; }

        public void Dispose() => Raster?.Dispose();
    }
}

internal sealed class NativeOwnedContent : ContentControl, IDisposable
{
    private readonly List<IDisposable> resources = [];

    public T Own<T>(T resource)
        where T : IDisposable
    {
        resources.Add(resource);
        return resource;
    }

    public void Dispose()
    {
        foreach (var resource in resources)
        {
            resource.Dispose();
        }
        resources.Clear();
        Content = null;
    }
}
