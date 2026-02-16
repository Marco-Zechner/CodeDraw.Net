using System.Collections.Concurrent;
using SkiaSharp;

namespace Legacy1.MarcoZechner.CodeDrawDotNet;

public static class ImageHandler
{
    // Optional: cache by path so multiple draws don’t re-decode PNGs
    private static readonly ConcurrentDictionary<string, ImageHandle> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Load an image from disk (cached by path).</summary>
    public static ImageHandle LoadImage(string filePath)
    {
        if (_imageCache.TryGetValue(filePath, out var cached))
            return cached;

        // FromEncodedData preserves PNG alpha, color profile etc.
        var data = SKData.Create(filePath);
        var skimg = SKImage.FromEncodedData(data) ?? throw new InvalidOperationException($"Failed to decode image: {filePath}");
        var handle = new ImageHandle(skimg);
        _imageCache[filePath] = handle;
        return handle;
    }

    /// <summary>Remove a single image from the cache and dispose it.</summary>
    public static void UnloadImage(string filePath)
    {
        if (_imageCache.TryRemove(filePath, out var handle))
            handle.Dispose();
    }

    /// <summary>Clear image cache (disposing all).</summary>
    public static void ClearImageCache()
    {
        foreach (var kv in _imageCache)
            kv.Value.Dispose();
        _imageCache.Clear();
    }
}
