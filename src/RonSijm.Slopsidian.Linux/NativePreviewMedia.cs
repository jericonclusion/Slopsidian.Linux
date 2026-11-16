using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Syntax;
using SkiaSharp;

namespace RonSijm.Slopsidian.Linux;

public static class NativePreviewMedia
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();

    public static bool IsRaster(string extension) => extension.ToLowerInvariant() is "png" or "jpg" or "jpeg" or "gif" or "webp" or "avif" or "bmp" or "ico";
    public static bool IsText(string extension) => extension.ToLowerInvariant() is "md" or "markdown" or "txt" or "canvas" or "base" or "json" or "yaml" or "yml" or "css" or "csv" or "xml" or "svg" or "html" or "htm";

    public static string DecodeText(byte[] bytes)
    {
        var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? 3 : 0;
        return Utf8.GetString(bytes, offset, bytes.Length - offset);
    }

    public static string Section(string text, string subpath)
    {
        if (string.IsNullOrEmpty(subpath)) { return text; }
        var fragment = Uri.UnescapeDataString(subpath.TrimStart('#'));
        if (fragment.StartsWith('^')) { return Block(text, fragment[1..]); }
        var headings = NativeMetadata.Parse("", text).Headings;
        var heading = headings.FirstOrDefault(item => item.Title.Equals(fragment, StringComparison.OrdinalIgnoreCase)
            || Slug(item.Title).Equals(fragment, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("This heading was not found in the embedded note.");
        var next = headings.FirstOrDefault(item => item.Offset > heading.Offset && item.Level <= heading.Level);
        return text[heading.Offset..(next?.Offset ?? text.Length)];
    }

    private static string Slug(string title) => title.Trim().Replace(' ', '-');

    private static string Block(string text, string id)
    {
        if (!Regex.IsMatch(id, @"\A[\w-]{1,128}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) { throw new InvalidDataException("This block identifier is invalid."); }
        var matches = Regex.Matches(text, @"(?m)(?<![\w-])\^" + Regex.Escape(id) + @"[ \t]*\r?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var document = Markdown.Parse(text, Pipeline);
        foreach (Match marker in matches)
        {
            if (Escaped(text, marker.Index)) { continue; }
            var block = document.Descendants<LeafBlock>().FirstOrDefault(item => item is not CodeBlock && item is not Markdig.Extensions.Yaml.YamlFrontMatterBlock && item.Span.Start <= marker.Index && item.Span.End >= marker.Index);
            if (block is null) { continue; }
            var start = block.Span.Start;
            var end = block.Span.End + 1;
            if (text[start..end].Trim() == "^" + id)
            {
                var previous = document.LastOrDefault(item => item.Span.End < marker.Index);
                if (previous is not null) { start = previous.Span.Start; }
            }
            return text[start..end].Remove(marker.Index - start, Math.Min(marker.Length, end - marker.Index)).TrimEnd();
        }
        throw new InvalidDataException("This block was not found in the embedded note.");
    }

    private static bool Escaped(string text, int offset)
    {
        var count = 0;
        for (var index = offset - 1; index >= 0 && text[index] == '\\'; index--) { count++; }
        return count % 2 != 0;
    }

    public static NativeDecodedRaster DecodeRaster(byte[] bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("This file is not a supported raster image.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 32768 || info.Height > 32768 || (long)info.Width * info.Height > 64_000_000)
        { throw new InvalidDataException("This image is too large to preview."); }
        using var stream = new MemoryStream(bytes, false);
        var bitmap = info.Width >= info.Height
            ? Bitmap.DecodeToWidth(stream, Math.Min(info.Width, 1024), BitmapInterpolationMode.MediumQuality)
            : Bitmap.DecodeToHeight(stream, Math.Min(info.Height, 1024), BitmapInterpolationMode.MediumQuality);
        if (cancellationToken.IsCancellationRequested) { bitmap.Dispose(); cancellationToken.ThrowIfCancellationRequested(); }
        return new NativeDecodedRaster(bitmap, new PixelSize(info.Width, info.Height));
    }
}

public sealed record NativeDecodedRaster(Bitmap Bitmap, PixelSize OriginalSize) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}

internal sealed class NativePreviewBudget
{
    // The process shares two read/decode slots; each Canvas retains at most 24 previews.
    public static SemaphoreSlim LoadSlots { get; } = new(2, 2);
    private int retained;
    public bool Acquire()
    {
        if (retained >= 24) { return false; }
        retained++;
        return true;
    }
    public void Release() => retained--;
}
