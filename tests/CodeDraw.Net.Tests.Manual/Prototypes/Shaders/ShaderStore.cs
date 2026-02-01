using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class ShaderStore
{
    private sealed class Entry(string vertPath, string fragPath)
    {
        public readonly string VertPath = vertPath;
        public readonly string FragPath = fragPath;
        public readonly string Label = Path.GetFileNameWithoutExtension(vertPath);   // derived from filename
        public uint Program;
        public DateTime LastVertWriteUtc;
        public DateTime LastFragWriteUtc;
        public volatile bool Dirty = true;
        public readonly object BuildLock = new();
        public string? LastError; // optional: keep last compile error to show in UI
        public FileSystemWatcher? WatcherVert;
        public FileSystemWatcher? WatcherFrag;
    }

    private static readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get or create a shader program from two files. If hotReload=true, file changes trigger Dirty.
    /// Call GetProgram each frame before using, it will rebuild when Dirty.
    /// </summary>
    public static uint GetProgram(GL gl, string vertPath, string fragPath, bool hotReload = true)
    {
        var key = $"{Path.GetFullPath(vertPath)}|{Path.GetFullPath(fragPath)}";
        var e = _entries.GetOrAdd(key, _ => new Entry(vertPath, fragPath));

        if (hotReload) EnsureWatchers(e);

        // Cheap timestamp check as an extra safety net (watchers can miss events)
        var vWrite = File.GetLastWriteTimeUtc(e.VertPath);
        var fWrite = File.GetLastWriteTimeUtc(e.FragPath);
        if (vWrite != e.LastVertWriteUtc || fWrite != e.LastFragWriteUtc)
            e.Dirty = true;

        if (!e.Dirty && e.Program != 0)
            return e.Program;

        lock (e.BuildLock)
        {
            // re-check inside lock
            vWrite = File.GetLastWriteTimeUtc(e.VertPath);
            fWrite = File.GetLastWriteTimeUtc(e.FragPath);
            if (!e.Dirty && e.Program != 0 && vWrite == e.LastVertWriteUtc && fWrite == e.LastFragWriteUtc)
                return e.Program;

            try
            {
                var vs = File.ReadAllText(e.VertPath);
                var fs = File.ReadAllText(e.FragPath);

                var newProg = ShaderCompiler.CreateProgram(gl, vs, fs, e.Label);

                // Swap program
                if (e.Program != 0) gl.DeleteProgram(e.Program);
                e.Program = newProg;

                e.LastVertWriteUtc = vWrite;
                e.LastFragWriteUtc = fWrite;
                e.Dirty = false;
                e.LastError = null;

                return e.Program;
            }
            catch (Exception ex)
            {
                // Keep old program if compile fails, so your app keeps running.
                e.LastError = ex.ToString();
                e.Dirty = false; // avoid spamming compile every frame; changes will set Dirty again
                return e.Program; // might be 0 if it never compiled successfully
            }
        }
    }

    public static string? GetLastError(string vertPath, string fragPath)
    {
        var key = $"{Path.GetFullPath(vertPath)}|{Path.GetFullPath(fragPath)}";
        return _entries.TryGetValue(key, out var e) ? e.LastError : null;
    }

    public static void DisposeAll(GL gl)
    {
        foreach (var e in _entries.Values)
        {
            if (e.Program != 0) gl.DeleteProgram(e.Program);
            e.Program = 0;
            e.WatcherVert?.Dispose();
            e.WatcherFrag?.Dispose();
        }
        _entries.Clear();
    }

    private static void EnsureWatchers(Entry e)
    {
        if (e.WatcherVert != null && e.WatcherFrag != null) return;

        void MarkDirty()
        {
            // Mark dirty; rebuild will happen on next GetProgram().
            e.Dirty = true;
        }

        e.WatcherVert ??= CreateWatcher(e.VertPath, MarkDirty);
        e.WatcherFrag ??= CreateWatcher(e.FragPath, MarkDirty);
    }

    private static FileSystemWatcher CreateWatcher(string filePath, Action onChange)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var name = Path.GetFileName(filePath);

        var w = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName
        };

        FileSystemEventHandler handler = (_, __) => onChange();
        RenamedEventHandler handlerRen = (_, __) => onChange();

        w.Changed += handler;
        w.Created += handler;
        w.Deleted += handler;
        w.Renamed += handlerRen;

        w.EnableRaisingEvents = true;
        return w;
    }
}
