using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RonSijm.Slopsidian.Linux;

public sealed class NativeImagePreviewWindow : Window
{
    public NativeImagePreviewWindow(Window owner, NativeWorkspace workspace, string path)
    {
        if (!NativePreviewMedia.IsRaster(System.IO.Path.GetExtension(path).TrimStart('.'))) { throw new ArgumentException("Select a raster image to preview.", nameof(path)); }
        Icon = owner.Icon;
        Title = System.IO.Path.GetFileName(path) + " — Image preview";
        Width = 1000; Height = 800; MinWidth = 480; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Preview = new NativeFilePreview(workspace, path, "", RejectNavigation);
        var layout = new Grid { Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 16 };
        layout.Children.Add(new TextBlock { Text = path, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(Preview, 1); layout.Children.Add(Preview);
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2); layout.Children.Add(close);
        Content = layout;
        Closed += (_, _) => Preview.Dispose();
    }

    public NativeFilePreview Preview { get; }
    private static void RejectNavigation(string source, string target, bool markdown) => throw new InvalidOperationException("An image preview cannot follow note links.");
}
