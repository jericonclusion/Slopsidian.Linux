using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using SkiaSharp;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class FilePreviewTests
{
    private const string Note = "---\nprivate: hidden value\n---\n# First\nIntro\n\n## Wanted section\nKeep this.\n\n### Child\nAlso keep this.\n\n## Next\nExclude this.\n\nA block\nwith two lines. ^block\n\nAfter block.\n\n```\n^in-code\n```\n\nEscaped \\^escaped\n";

    [Theory]
    [InlineData("#Wanted%20section")]
    [InlineData("#wanted-section")]
    public void HeadingPreviewIncludesItsChildrenAndStopsAtTheNextPeer(string subpath)
    {
        var section = NativePreviewMedia.Section(Note, subpath);
        Assert.StartsWith("## Wanted section", section);
        Assert.Contains("Also keep this.", section);
        Assert.DoesNotContain("Exclude this.", section);
        Assert.DoesNotContain("private:", section);
    }

    [Fact]
    public void BlockPreviewReturnsTheWholeParagraphWithoutItsIdentifier()
    {
        Assert.Equal("A block\nwith two lines.", NativePreviewMedia.Section(Note, "#^block"));
        Assert.Equal(Note, NativePreviewMedia.Section(Note, ""));
    }

    [Theory]
    [InlineData("#Missing")]
    [InlineData("#^missing")]
    [InlineData("#^in-code")]
    [InlineData("#^escaped")]
    public void MissingCodeAndEscapedBlockTargetsAreNotRenderedAsASection(string subpath) =>
        Assert.Throws<InvalidDataException>(() => NativePreviewMedia.Section(Note, subpath));

    [Fact]
    public void PreviewTextStripsTheBomAndRejectsInvalidUtf8()
    {
        Assert.Equal("Café\r\n", NativePreviewMedia.DecodeText(Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes("Café\r\n")).ToArray()));
        Assert.Throws<DecoderFallbackException>(() => NativePreviewMedia.DecodeText([0xff]));
    }

    [Fact]
    public void RasterPreviewRejectsInvalidOversizedAndCancelledImagesBeforeRendering()
    {
        Assert.Throws<InvalidDataException>(() => NativePreviewMedia.DecodeRaster([1, 2, 3], TestContext.Current.CancellationToken));
        var error = Assert.Throws<InvalidDataException>(() => NativePreviewMedia.DecodeRaster(Png(32769, 1), TestContext.Current.CancellationToken));
        Assert.Contains("too large", error.Message);
        Assert.ThrowsAny<OperationCanceledException>(() => NativePreviewMedia.DecodeRaster(Png(), new CancellationToken(true)));
    }

    [AvaloniaFact]
    public async Task EmbeddedNoteUsesItsOwnLinkContextAndFollowsAnOpenDraftWithoutSavingIt()
    {
        var directory = TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "Folder"));
        var path = Path.Combine(directory, "Folder", "Note.md");
        const string saved = "---\nprivate: hidden\n---\n# Embedded note\n[Go](../Target.md)\n";
        await File.WriteAllTextAsync(path, saved, TestContext.Current.CancellationToken);
        using var workspace = new NativeWorkspace();
        await workspace.OpenVaultAsync(directory);
        (string Source, string Link, bool Markdown)? followed = null;
        using var preview = new NativeFilePreview(workspace, "Folder/Note.md", "", (source, link, markdown) => followed = (source, link, markdown));
        var window = new Window { Content = preview, Width = 600, Height = 500 };
        try
        {
            window.Show(); await preview.Loading;
            Assert.True(preview.HasLoadedContent, preview.Failure);
            Assert.DoesNotContain("private:", VisibleText(preview));
            preview.GetVisualDescendants().OfType<Button>().Single(button => button.Content is TextBlock).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(("Folder/Note.md", "../Target.md", true), followed);
            var draft = await workspace.OpenTabAsync("Folder/Note.md");
            draft.Text = "# Live draft\nUnsaved content";
            await preview.Loading;
            Assert.Contains("Live draft", VisibleText(preview));
            Assert.Equal(saved, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            window.Content = null;
            Assert.False(preview.HasLoadedContent);
            draft.Text = "# Changed while detached";
            window.Content = preview;
            await preview.Loading;
            Assert.True(preview.HasLoadedContent, preview.Failure);
            Assert.Contains("Changed while detached", VisibleText(preview));
        }
        finally { window.Close(); await preview.Loading; Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task RasterPreviewReleasesItsImageAndRefusesToDisplayAnOldVaultAfterSwitching()
    {
        var directory = TemporaryDirectory();
        var other = Path.Combine(directory, "Other"); Directory.CreateDirectory(other);
        var bytes = Png();
        await File.WriteAllBytesAsync(Path.Combine(directory, "Picture.png"), bytes, TestContext.Current.CancellationToken);
        using var workspace = new NativeWorkspace();
        await workspace.OpenVaultAsync(directory);
        using var preview = new NativeFilePreview(workspace, "Picture.png", "", (_, _, _) => throw new InvalidOperationException("An image preview should not navigate automatically."));
        var window = new Window { Content = preview, Width = 600, Height = 500 };
        try
        {
            window.Show(); await preview.Loading;
            Assert.True(preview.HasLoadedContent, preview.Failure);
            Assert.NotNull(preview.GetVisualDescendants().OfType<Image>().Single().Source);
            await workspace.OpenVaultAsync(other);
            await preview.Loading;
            Assert.False(preview.HasLoadedContent);
            Assert.Contains("previous vault", preview.Failure);
            Assert.Empty(preview.GetVisualDescendants().OfType<Image>());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(directory, "Picture.png"), TestContext.Current.CancellationToken));
        }
        finally { window.Close(); await preview.Loading; Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task CanvasFileAndBackgroundPreviewsDoNotEditSourceAndAreDisposedWhenCardsAreRemoved()
    {
        var directory = TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(directory, "Picture.png"), Png(), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "Note.md"), Note, TestContext.Current.CancellationToken);
        const string scene = """
            {"nodes":[
              {"id":"group","type":"group","x":0,"y":0,"width":1000,"height":500,"background":"Picture.png","backgroundStyle":"repeat","label":"Project"},
              {"id":"note","type":"file","x":40,"y":80,"width":400,"height":300,"file":"Note.md","subpath":"#Wanted section"},
              {"id":"picture","type":"file","x":540,"y":80,"width":400,"height":300,"file":"Picture.png"}
            ],"edges":[]}
            """;
        await File.WriteAllTextAsync(Path.Combine(directory, "Board.canvas"), scene, TestContext.Current.CancellationToken);
        using var profile = new NativeProfile();
        profile.State = profile.State with { Preferences = new NativePreferences { Autosave = false } };
        var window = new MainWindow(profile);
        NativeFilePreview[] previews = [];
        try
        {
            window.Show(); await window.OpenVaultAsync(directory);
            var tree = window.FindControl<TreeView>("FileTree")!;
            tree.SelectedItem = tree.ItemsSource!.Cast<NativeFileNode>().Single(node => node.Path == "Board.canvas");
            for (var index = 0; window.Workspace.ActiveTab?.Path != "Board.canvas" && index < 500; index++) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            window.UpdateLayout();
            previews = window.GetVisualDescendants().OfType<NativeFilePreview>().ToArray();
            Assert.Equal(3, previews.Length);
            await Task.WhenAll(previews.Select(preview => preview.Loading));
            Assert.All(previews, preview => Assert.True(preview.HasLoadedContent, preview.Failure));
            Assert.Equal(scene, window.Workspace.Text);
            Assert.False(window.Workspace.IsDirty);
            window.FindControl<TextEditor>("Editor")!.Text = "{\"nodes\":[],\"edges\":[]}";
            Assert.All(previews, preview => { Assert.True(preview.IsDisposed); Assert.False(preview.HasLoadedContent); });
            Assert.Equal(scene, await File.ReadAllTextAsync(Path.Combine(directory, "Board.canvas"), TestContext.Current.CancellationToken));
        }
        finally { window.Workspace.DiscardEdits(); window.Close(); await Task.WhenAll(previews.Select(preview => preview.Loading)); Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task SelectingAnImageInTheFileTreeOpensANativePreviewAndKeepsItsExactBytes()
    {
        var directory = TemporaryDirectory();
        var bytes = Png();
        await File.WriteAllBytesAsync(Path.Combine(directory, "Picture.png"), bytes, TestContext.Current.CancellationToken);
        using var profile = new NativeProfile();
        var window = new MainWindow(profile);
        NativeImagePreviewWindow? image = null;
        try
        {
            window.Show(); await window.OpenVaultAsync(directory);
            var tree = window.FindControl<TreeView>("FileTree")!;
            tree.SelectedItem = tree.ItemsSource!.Cast<NativeFileNode>().Single();
            for (var index = 0; !window.OwnedWindows.OfType<NativeImagePreviewWindow>().Any() && index < 500; index++) { await Task.Delay(10, TestContext.Current.CancellationToken); }
            image = Assert.Single(window.OwnedWindows.OfType<NativeImagePreviewWindow>());
            await image.Preview.Loading;
            Assert.True(image.Preview.HasLoadedContent, image.Preview.Failure);
            Assert.Empty(window.Workspace.Tabs);
            image.Close();
            Assert.True(image.Preview.IsDisposed);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(directory, "Picture.png"), TestContext.Current.CancellationToken));
        }
        finally { image?.Close(); window.Close(); if (image is not null) { await image.Preview.Loading; } Directory.Delete(directory, true); }
    }

    private static string VisibleText(Control control) => string.Join("\n", control.GetVisualDescendants().OfType<SelectableTextBlock>()
        .Select(block => block.Text ?? string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? [])));

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "slopsidian-file-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    private static byte[] Png(int width = 8, int height = 6)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
