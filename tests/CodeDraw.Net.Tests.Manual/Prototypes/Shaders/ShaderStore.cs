using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public sealed class ShaderStore : IDisposable
{
    private sealed class Entry
    {
        public readonly string Name;
        public uint Program;
        public DateTime LastVertWriteUtc;
        public DateTime LastFragWriteUtc;
        public volatile bool Dirty = true;
        public string? LastError;
        public readonly object BuildLock = new();

        public Entry(string name) => Name = name;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public string RootPath { get; }
    public bool HotReload { get; set; }

    // Owned by store
    public UniformCache Uniforms { get; } = new();

    private readonly IGlExecutor _exec;

    // One directory watcher for robust reload
    private FileSystemWatcher? _watcher;
    private readonly object _watchLock = new();

    public ShaderStore(string rootPath, IGlExecutor exec, bool hotReload = true)
    {
        RootPath = Path.GetFullPath(rootPath);
        HotReload = hotReload;
        _exec = exec;

        if (HotReload) EnsureWatcher();
    }

    public uint GetProgram(string name)
    {
        var e = _entries.GetOrAdd(name, n => new Entry(n));
        if (HotReload) EnsureWatcher();

        var vertAbs = Path.Combine(RootPath, name + ".vert");
        var fragAbs = Path.Combine(RootPath, name + ".frag");

        var vWrite = File.Exists(vertAbs) ? File.GetLastWriteTimeUtc(vertAbs) : DateTime.MinValue;
        var fWrite = File.Exists(fragAbs) ? File.GetLastWriteTimeUtc(fragAbs) : DateTime.MinValue;

        if (vWrite != e.LastVertWriteUtc || fWrite != e.LastFragWriteUtc)
            e.Dirty = true;

        if (!e.Dirty && e.Program != 0) return e.Program;

        lock (e.BuildLock)
        {
            // re-check under lock
            vWrite = File.Exists(vertAbs) ? File.GetLastWriteTimeUtc(vertAbs) : DateTime.MinValue;
            fWrite = File.Exists(fragAbs) ? File.GetLastWriteTimeUtc(fragAbs) : DateTime.MinValue;

            if (!e.Dirty && e.Program != 0 && vWrite == e.LastVertWriteUtc && fWrite == e.LastFragWriteUtc)
                return e.Program;

            try
            {
                if (!File.Exists(vertAbs) || !File.Exists(fragAbs))
                    throw new FileNotFoundException($"Shader files not found for '{name}' in '{RootPath}'.");

                Console.WriteLine($"[ShaderStore] {(e.Program != 0 ? "Rec" : "C")}ompiling shader program: " + name);

                var vs = File.ReadAllText(vertAbs);
                var fs = File.ReadAllText(fragAbs);

                // Compile/link on compiler context
                var newProg = _exec.Run(gl => ShaderCompiler.CreateProgram(gl, vs, fs, label: name));

                // Swap old -> new, delete old on compiler context
                var old = e.Program;
                e.Program = newProg;

                if (old != 0)
                {
                    Uniforms.Invalidate(old);
                    _exec.Run(gl => gl.DeleteProgram(old));
                }

                e.LastVertWriteUtc = vWrite;
                e.LastFragWriteUtc = fWrite;
                e.Dirty = false;
                e.LastError = null;

                return e.Program;
            }
            catch (Exception ex)
            {
                e.LastError = ex.ToString();
                e.Dirty = false; // don’t spam compile every call; next file change re-dirties
                return e.Program; // might be 0 if never succeeded
            }
        }
    }

    public int GetUniformLocation(GL gl, uint program, string uniformName)
        => Uniforms.Get(gl, program, uniformName);

    public string? GetLastError(string name)
        => _entries.TryGetValue(name, out var e) ? e.LastError : null;

    private void EnsureWatcher()
    {
        lock (_watchLock)
        {
            if (_watcher != null) return;

            var w = new FileSystemWatcher(RootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName
            };

            void Touch(string path)
            {
                var file = Path.GetFileName(path);
                if (file is null) return;

                if (!file.EndsWith(".vert", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".frag", StringComparison.OrdinalIgnoreCase))
                    return;

                var baseName = Path.GetFileNameWithoutExtension(file);
                if (_entries.TryGetValue(baseName, out var e))
                    e.Dirty = true;
            }

            FileSystemEventHandler onChange = (_, ev) => Touch(ev.FullPath);
            RenamedEventHandler onRename = (_, ev) => { Touch(ev.OldFullPath); Touch(ev.FullPath); };

            w.Changed += onChange;
            w.Created += onChange;
            w.Deleted += onChange;
            w.Renamed += onRename;

            w.EnableRaisingEvents = true;
            _watcher = w;
        }
    }

    public void Dispose()
    {
        lock (_watchLock)
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        // Programs must be deleted on compiler context
        foreach (var e in _entries.Values)
        {
            var p = e.Program;
            if (p != 0)
            {
                Uniforms.Invalidate(p);
                _exec.Run(gl => gl.DeleteProgram(p));
                e.Program = 0;
            }
        }

        _entries.Clear();
        Uniforms.Clear();
    }
}