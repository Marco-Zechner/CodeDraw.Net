using System.Collections.Concurrent;
using MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;
using Silk.NET.OpenGL;

public sealed class ShaderStore : IDisposable
{
    private sealed class Entry
    {
        public string Name = "";
        public string VertPath = "";
        public string FragPath = "";

        // Written by watcher thread, read by GL thread
        public volatile int LatestSourceVersion;
        public string? PendingVS;
        public string? PendingFS;

        // Owned by GL thread
        public uint Program;
        public int CompiledVersion;

        public string? LastError;

        // Prevent concurrent GL compiles (still GL thread, but protects mistakes)
        public readonly object CompileLock = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _rootDir;
    private readonly string _label;

    private FileSystemWatcher? _watcher;
    private readonly object _watchLock = new();

    // delete queue must be executed on GL thread
    private readonly ConcurrentQueue<uint> _deletePrograms = new();

    public ShaderStore(string shaderRootDirectory, string label, bool hotReload = true)
    {
        _label = label;
        _rootDir = Path.GetFullPath(shaderRootDirectory);
        if (hotReload) EnsureWatcher();
    }

    private Entry GetOrCreate(string name)
    {
        var vertPath = Path.Combine(_rootDir, name + ".vert");
        var fragPath = Path.Combine(_rootDir, name + ".frag");

        return _entries.GetOrAdd(name, n => new Entry
        {
            Name = n,
            VertPath = vertPath,
            FragPath = fragPath,
            LatestSourceVersion = 0,
            CompiledVersion = -1, // force initial compile
        });
    }

    public uint GetProgram(string name)
    {
        // PURE GETTER: no GL calls, no file IO, no compilation.
        // Program is swapped only in BeginFrame().
        var e = GetOrCreate(name);
        return e.Program;
    }

    public int GetUniformLocation(GL gl, uint program, string uniformName)
        => program == 0 ? -1 : gl.GetUniformLocation(program, uniformName);

    // Call on GL thread at safe point (start of frame / start of DrainUntil)
    public void BeginFrame(GL gl)
    {
        // 1) apply pending compiles
        foreach (var kv in _entries)
        {
            var e = kv.Value;

            var latest = e.LatestSourceVersion;
            if (latest == e.CompiledVersion) continue;

            lock (e.CompileLock)
            {
                latest = e.LatestSourceVersion;
                if (latest == e.CompiledVersion) continue;

                // Need sources. If watcher didn't populate yet, load once here.
                // (Only happens on first compile or if watcher missed.)
                var vs = e.PendingVS ?? File.ReadAllText(e.VertPath);
                var fs = e.PendingFS ?? File.ReadAllText(e.FragPath);

                try
                {
                    Console.WriteLine($"[ShaderStore:{_label}] {(e.Program != 0 ? "Rec" : "C")}ompiling shader program: {e.Name}");
                    var newProg = ShaderCompiler.CreateProgram(gl, vs, fs, label: e.Name);

                    var old = e.Program;
                    e.Program = newProg;
                    e.CompiledVersion = latest;
                    e.LastError = null;

                    if (old != 0)
                        _deletePrograms.Enqueue(old);
                }
                catch (Exception ex)
                {
                    e.LastError = ex.ToString();
                    // Important: do NOT advance CompiledVersion on failure,
                    // so next BeginFrame retries after next edit (or you can keep a "failedVersion" if desired)
                }
            }
        }

        // 2) delete old programs after all swaps
        while (_deletePrograms.TryDequeue(out var p))
        {
            if (p != 0) gl.DeleteProgram(p);
        }
    }

    public string? GetLastError(string name)
        => _entries.TryGetValue(name, out var e) ? e.LastError : null;

    private void EnsureWatcher()
    {
        lock (_watchLock)
        {
            if (_watcher != null) return;

            var w = new FileSystemWatcher(_rootDir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            void OnAny(string fullPath)
            {
                var file = Path.GetFileName(fullPath);
                if (string.IsNullOrWhiteSpace(file)) return;

                if (!file.EndsWith(".vert", StringComparison.OrdinalIgnoreCase) &&
                    !file.EndsWith(".frag", StringComparison.OrdinalIgnoreCase))
                    return;

                var name = Path.GetFileNameWithoutExtension(file);
                var e = GetOrCreate(name);

                // Read both files NOW on watcher thread so GL thread doesn't do file IO
                // If editor is mid-write, this can throw; that's fine: just don't bump version.
                try
                {
                    if (!File.Exists(e.VertPath) || !File.Exists(e.FragPath))
                        return;

                    var vs = File.ReadAllText(e.VertPath);
                    var fs = File.ReadAllText(e.FragPath);

                    e.PendingVS = vs;
                    e.PendingFS = fs;

                    // bump version last (publish step)
                    Interlocked.Increment(ref e.LatestSourceVersion);
                }
                catch
                {
                    // ignore transient write states; next change event will succeed
                }
            }

            FileSystemEventHandler h = (_, ev) => OnAny(ev.FullPath);
            RenamedEventHandler r = (_, ev) => { OnAny(ev.OldFullPath); OnAny(ev.FullPath); };

            w.Changed += h;
            w.Created += h;
            w.Deleted += h;
            w.Renamed += r;

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
        // programs must be deleted on GL thread; do it via DisposePrograms(gl)
    }

    public void DisposePrograms(GL gl)
    {
        while (_deletePrograms.TryDequeue(out var p))
            if (p != 0) gl.DeleteProgram(p);

        foreach (var e in _entries.Values)
        {
            if (e.Program != 0) gl.DeleteProgram(e.Program);
            e.Program = 0;
        }
        _entries.Clear();
    }
}
