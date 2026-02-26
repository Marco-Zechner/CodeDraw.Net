using System.Collections.Concurrent;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace MarcoZechner.CodeDrawDotNet.Images;

public static class ImageStore
{
    private sealed class ImageEntry
    {
        public ImageKey Key;

        public uint Tex;
        public int W;
        public int H;

        public long BuiltWriteTicks = -1;

        public string? LastError;
        public string? LastErrorReported;
    }

    private readonly record struct ImageMeta(int W, int H, long WriteTicks);

    // Global: key -> last known metadata (independent of GL consumer)
    private static readonly ConcurrentDictionary<ImageKey, ImageMeta> _meta = new();

    private static readonly ConcurrentDictionary<Shaders.IShaderConsumer, ConcurrentDictionary<ImageKey, ImageEntry>> _byConsumer = new();
    private static readonly ConcurrentDictionary<Shaders.IShaderConsumer, object> _consumerLocks = new();

    public static bool TryGetMeta(ImageKey key, out int w, out int h)
    {
        if (_meta.TryGetValue(key, out var m))
        {
            w = m.W;
            h = m.H;
            return true;
        }

        w = h = 0;
        return false;
    }

    public static void Register(Shaders.IShaderConsumer consumer, ImageKey key)
    {
        var dict = _byConsumer.GetOrAdd(consumer, _ => new ConcurrentDictionary<ImageKey, ImageEntry>());
        if (dict.ContainsKey(key)) return;

        dict.TryAdd(key, new ImageEntry
        {
            Key = key,
            Tex = 0,
            W = 0,
            H = 0,
            BuiltWriteTicks = -1
        });
    }

    public static unsafe uint GetTexture(
        GL gl,
        Shaders.IShaderConsumer consumer,
        ImageKey key,
        out int w,
        out int h)
    {
        w = h = 0;

        if (!_byConsumer.TryGetValue(consumer, out var dict)) return 0;
        if (!dict.TryGetValue(key, out var e)) return 0;

        try
        {
            if (!File.Exists(key.AbsPath))
            {
                var msg = $"File missing: '{key.AbsPath}'";
                e.LastError = msg;
                ReportOnce(consumer, e, msg);
                return 0;
            }

            var writeTicks = File.GetLastWriteTimeUtc(key.AbsPath).Ticks;

            if (e.Tex != 0 && e.BuiltWriteTicks == writeTicks)
            {
                w = e.W; h = e.H;
                return e.Tex;
            }

            var fileBytes = File.ReadAllBytes(key.AbsPath);

            ImageResult img;
            try
            {
                img = ImageResult.FromMemory(fileBytes, ColorComponents.RedGreenBlueAlpha);
            }
            catch (Exception ex)
            {
                var msg = $"Decode failed: '{key.AbsPath}'\n{ex}";
                e.LastError = msg;
                ReportOnce(consumer, e, msg);
                return 0;
            }

            if (img.Width <= 0 || img.Height <= 0 || img.Data == null || img.Data.Length == 0)
            {
                var msg = $"Decode produced empty image: '{key.AbsPath}'";
                e.LastError = msg;
                ReportOnce(consumer, e, msg);
                return 0;
            }

            if (e.Tex == 0)
                e.Tex = gl.GenTexture();

            gl.BindTexture(GLEnum.Texture2D, e.Tex);

            gl.PixelStore(GLEnum.UnpackAlignment, 1);

            fixed (byte* p = img.Data)
            {
                gl.TexImage2D(
                    target: GLEnum.Texture2D,
                    level: 0,
                    internalformat: (int)GLEnum.Rgba8,
                    width: (uint)img.Width,
                    height: (uint)img.Height,
                    border: 0,
                    format: GLEnum.Rgba,
                    type: GLEnum.UnsignedByte,
                    pixels: p
                );
            }

            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

            gl.BindTexture(GLEnum.Texture2D, 0);

            e.W = img.Width;
            e.H = img.Height;
            e.BuiltWriteTicks = writeTicks;

            // publish metadata globally
            _meta[key] = new ImageMeta(e.W, e.H, writeTicks);

            e.LastError = null;
            e.LastErrorReported = null;

            w = e.W; h = e.H;
            Console.WriteLine($"[Info] ImageStore:{consumer.DebugName} Loaded '{key.AbsPath}' ({e.W}x{e.H})");
            return e.Tex;
        }
        catch (Exception ex)
        {
            var msg = $"Upload failed: '{key.AbsPath}'\n{ex}";
            e.LastError = msg;
            ReportOnce(consumer, e, msg);
            return 0;
        }
    }

    public static void DisposeConsumer(GL gl, Shaders.IShaderConsumer consumer)
    {
        if (!_byConsumer.TryRemove(consumer, out var dict)) return;

        var lockObj = _consumerLocks.GetOrAdd(consumer, _ => new object());
        lock (lockObj)
        {
            foreach (var kv in dict)
            {
                var e = kv.Value;
                if (e.Tex != 0) gl.DeleteTexture(e.Tex);
                e.Tex = 0;
            }
        }

        _consumerLocks.TryRemove(consumer, out _);
    }

    private static void ReportOnce(Shaders.IShaderConsumer consumer, ImageEntry e, string msg)
    {
        if (string.Equals(e.LastErrorReported, msg, StringComparison.Ordinal)) return;
        e.LastErrorReported = msg;
        Console.WriteLine($"[Error] ImageStore:{consumer.DebugName} {msg}");
    }
}