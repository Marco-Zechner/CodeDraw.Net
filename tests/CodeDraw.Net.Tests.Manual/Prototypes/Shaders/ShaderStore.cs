using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// Per-context shader program store. DO NOT share between different GL contexts.
/// It compiles programs from files: {Root}/{name}.vert + {Root}/{name}.frag
/// </summary>
public sealed class ShaderStore : IDisposable
{
    private sealed class Entry(string name)
    {
        public readonly string Name = name;
        public uint Program;
        public DateTime LastVertWriteUtc;
        public DateTime LastFragWriteUtc;
        public bool Dirty = true;
        public readonly object BuildLock = new();
        public string? LastError;
    }

    private readonly GL _gl;
    private readonly ConcurrentDictionary<string, Entry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public string RootPath { get; }
    public bool HotReload { get; set; }

    public ShaderStore(GL gl, string rootPath, bool hotReload = false)
    {
        _gl = gl;
        RootPath = Path.GetFullPath(rootPath);
        HotReload = hotReload;

        if (!Directory.Exists(RootPath))
            Console.WriteLine($"[ShaderStore] Warning: shader root not found: {RootPath}");
    }

    public uint GetProgram(string name)
    {
        var e = _entries.GetOrAdd(name, n => new Entry(n));

        var vertAbs = Path.Combine(RootPath, name + ".vert");
        var fragAbs = Path.Combine(RootPath, name + ".frag");

        // HotReload "lite": timestamp check only (safe + no watchers required)
        // If files don't exist, we still try once to produce a useful error.
        DateTime vWrite = DateTime.MinValue, fWrite = DateTime.MinValue;
        if (HotReload && File.Exists(vertAbs) && File.Exists(fragAbs))
        {
            vWrite = File.GetLastWriteTimeUtc(vertAbs);
            fWrite = File.GetLastWriteTimeUtc(fragAbs);
            if (vWrite != e.LastVertWriteUtc || fWrite != e.LastFragWriteUtc)
                e.Dirty = true;
        }

        if (!e.Dirty && e.Program != 0)
            return e.Program;

        lock (e.BuildLock)
        {
            // Re-check under lock
            if (!e.Dirty && e.Program != 0)
                return e.Program;

            try
            {
                if (!File.Exists(vertAbs) || !File.Exists(fragAbs))
                    throw new FileNotFoundException(
                        $"Shader files not found for '{name}'. Expected:\n  {vertAbs}\n  {fragAbs}");

                // Optional log – matches your existing clue
                Console.WriteLine($"[ShaderStore] {(e.Program != 0 ? "Rec" : "C")}ompiling shader program: {name}");

                var vs = File.ReadAllText(vertAbs);
                var fs = File.ReadAllText(fragAbs);

                var newProg = ShaderCompiler.CreateProgram(_gl, vs, fs, label: name);

                if (e.Program != 0)
                    _gl.DeleteProgram(e.Program);

                e.Program = newProg;
                e.LastError = null;
                e.Dirty = false;

                if (HotReload)
                {
                    // Update timestamps only if enabled (otherwise no need)
                    e.LastVertWriteUtc = File.GetLastWriteTimeUtc(vertAbs);
                    e.LastFragWriteUtc = File.GetLastWriteTimeUtc(fragAbs);
                }

                return e.Program;
            }
            catch (Exception ex)
            {
                e.LastError = ex.ToString();
                e.Dirty = false; // avoid compile spam; set Dirty=true manually or touch files if HotReload
                return e.Program; // keep old program if it exists
            }
        }
    }

    public int GetUniformLocation(uint program, string uniformName)
    {
        if (program == 0) return -1;
        return _gl.GetUniformLocation(program, uniformName);
    }

    public string? GetLastError(string name)
        => _entries.TryGetValue(name, out var e) ? e.LastError : null;

    public void MarkDirty(string name)
    {
        if (_entries.TryGetValue(name, out var e))
            e.Dirty = true;
    }

    public void Dispose()
    {
        foreach (var e in _entries.Values)
        {
            if (e.Program != 0)
            {
                _gl.DeleteProgram(e.Program);
                e.Program = 0;
            }
        }
        _entries.Clear();
    }
}