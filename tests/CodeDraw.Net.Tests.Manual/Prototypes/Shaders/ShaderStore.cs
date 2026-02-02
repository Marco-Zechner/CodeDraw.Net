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
    private readonly string _shaderStoreDebugName;

    private FileSystemWatcher? _watcher;
    private readonly object _watchLock = new();

    // delete queue must be executed on GL thread
    private readonly ConcurrentQueue<uint> _deletePrograms = new();

    public ShaderStore(string shaderRootDirectory, string shaderStoreDebugName, bool hotReload = true)
    {
        _shaderStoreDebugName = shaderStoreDebugName;
        _rootDir = Path.GetFullPath(shaderRootDirectory);
        if (hotReload) EnsureWatcher();
    }

    public CodeDrawShader Load(string name)
    {
        var programName = Path.GetFileNameWithoutExtension(name);

        _ = GetOrCreate(name); // ensure entry exists (and watcher can track)
        return new CodeDrawShader(this, programName: programName, displayName: programName);
    }

    public CodeDrawShader Load(string vertFileName, string fragFileName)
    {
        var v = Path.GetFileNameWithoutExtension(vertFileName);
        var f = Path.GetFileNameWithoutExtension(fragFileName);

        // logical program name: "v__f"
        var programName = GetCombinedName(v, f);

        _ = GetOrCreate(vertFileName, fragFileName);  // ensure entry exists (and watcher can track)
        return new CodeDrawShader(this, programName: programName, displayName: programName);
    }

    private static string GetCombinedName(string vertName, string fragName)
    {
        if (string.Equals(vertName, fragName, StringComparison.OrdinalIgnoreCase))
            return vertName;
        return vertName + "__" + fragName;
    }

    private Entry GetOrCreate(string name)
    {
        var programName = Path.GetFileNameWithoutExtension(name);

        var vertPath = Path.Combine(_rootDir, programName + ".vert");
        var fragPath = Path.Combine(_rootDir, programName + ".frag");

        if (!Path.Exists(vertPath))
            Console.WriteLine($"[ShaderStore:{_shaderStoreDebugName}] Warning: Vertex shader file not found: {vertPath}");
        if (!Path.Exists(fragPath))
            Console.WriteLine($"[ShaderStore:{_shaderStoreDebugName}] Warning: Fragment shader file not found: {fragPath}");

        return _entries.GetOrAdd(programName, n => new Entry
        {
            Name = n,
            VertPath = vertPath,
            FragPath = fragPath,
            LatestSourceVersion = 0,
            CompiledVersion = -1, // force initial compile
        });
    }

    private Entry GetOrCreate(string vertName, string fragName)
    {
        var v = Path.GetFileNameWithoutExtension(vertName);
        var f = Path.GetFileNameWithoutExtension(fragName);

        var vertPath = Path.Combine(_rootDir, v + ".vert");
        var fragPath = Path.Combine(_rootDir, f + ".frag");

        if (!Path.Exists(vertPath))
            Console.WriteLine($"[ShaderStore:{_shaderStoreDebugName}] Warning: Vertex shader file not found: {vertPath}");
        if (!Path.Exists(fragPath))
            Console.WriteLine($"[ShaderStore:{_shaderStoreDebugName}] Warning: Fragment shader file not found: {fragPath}");

        return _entries.GetOrAdd(GetCombinedName(v, f), n => new Entry
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
        // Program is swapped only in BeginFrame().
        var e = GetOrCreate(name);
        return e.Program;
    }

    public uint GetProgram(string vertName, string fragName)
    {
        // Program is swapped only in BeginFrame().
        var e = GetOrCreate(vertName, fragName);
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
                    Console.WriteLine($"[ShaderStore:{_shaderStoreDebugName}] {(e.Program != 0 ? "Rec" : "C")}ompiling shader program: {e.Name}");
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
                fullPath = Path.GetFullPath(fullPath);

                if (!fullPath.EndsWith(".vert", StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.EndsWith(".frag", StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (var e in _entries.Values)
                {
                    // entry paths may differ from "name.vert/name.frag"
                    if (!PathEquals(e.VertPath, fullPath) && !PathEquals(e.FragPath, fullPath))
                        continue;

                    try
                    {
                        if (!File.Exists(e.VertPath) || !File.Exists(e.FragPath))
                            continue;

                        var vs = File.ReadAllText(e.VertPath);
                        var fs = File.ReadAllText(e.FragPath);

                        e.PendingVS = vs;
                        e.PendingFS = fs;

                        Interlocked.Increment(ref e.LatestSourceVersion);
                    }
                    catch
                    {
                        // ignore transient write states
                    }
                }

                return;

                static bool PathEquals(string a, string b)
                    => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
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
