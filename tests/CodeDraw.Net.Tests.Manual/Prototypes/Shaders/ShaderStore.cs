using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class ShaderStore
{
    private sealed class ProgramEntry
    {
        public ShaderKey Key;

        public FileCache.Handle VertFile = null!;
        public FileCache.Handle FragFile = null!;

        public uint Program;
        public int BuiltVertVersion = -1;
        public int BuiltFragVersion = -1;

        public string? LastError;

        public readonly ConcurrentQueue<uint> DeleteQueue = new();

        // reporting flags
        public bool DefaultLoadedReported;
        public bool FileLoadedReported; // first successful real file compile
    }

    private static readonly ConcurrentDictionary<IShaderConsumer, ConcurrentDictionary<ShaderKey, ProgramEntry>> _byConsumer = new();
    private static readonly ConcurrentDictionary<IShaderConsumer, object> _consumerLocks = new();

    public static void Register(IShaderConsumer consumer, ShaderKey key, bool hotReload = true)
    {
        var dict = _byConsumer.GetOrAdd(consumer, _ => new ConcurrentDictionary<ShaderKey, ProgramEntry>());
        if (dict.ContainsKey(key)) return;

        var vert = FileCache.Acquire(key.VertPath, hotReload);
        var frag = FileCache.Acquire(key.FragPath, hotReload);

        var e = new ProgramEntry
        {
            Key = key,
            VertFile = vert,
            FragFile = frag,
            Program = 0,
            BuiltVertVersion = -1,
            BuiltFragVersion = -1
        };

        if (!dict.TryAdd(key, e))
        {
            vert.Dispose();
            frag.Dispose();
        }
    }

    public static uint GetProgram(IShaderConsumer consumer, ShaderKey key)
    {
        if (!_byConsumer.TryGetValue(consumer, out var dict)) return 0;
        return dict.TryGetValue(key, out var e) ? e.Program : 0;
    }

    public static int GetUniformLocation(GL gl, uint program, string uniformName)
        => program == 0 ? -1 : gl.GetUniformLocation(program, uniformName);

    public static void CheckHotReload(GL gl, IShaderConsumer consumer)
    {
        if (!_byConsumer.TryGetValue(consumer, out var dict)) return;

        var lockObj = _consumerLocks.GetOrAdd(consumer, _ => new object());
        lock (lockObj)
        {
            foreach (var kv in dict)
            {
                var e = kv.Value;

                var (vs, vVer, vErr) = e.VertFile.Snapshot();
                var (fs, fVer, fErr) = e.FragFile.Snapshot();

                var filesBad = (vErr != null || fErr != null);

                if (filesBad)
                {
                    e.LastError = $"File error: vert='{vErr ?? "ok"}', frag='{fErr ?? "ok"}'";

                    if (e.Program != 0)
                        continue; // keep last good

                    if (DefaultShaderSources.TryGet(e.Key, out var dvs, out var dfs))
                    {
                        if (!e.DefaultLoadedReported)
                        {
                            e.DefaultLoadedReported = true;
                            Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} loaded default for '{e.Key}' due to file error\n{e.LastError}");
                        }

                        TrySwapProgram(gl, consumer, e, dvs, dfs,
                            builtVertVersion: vVer, builtFragVersion: fVer, isDefault: true);
                    }

                    continue;
                }

                // No file errors: we can compile from files.

                // If nothing changed since last successful file-build, skip.
                if (vVer == e.BuiltVertVersion && fVer == e.BuiltFragVersion)
                    continue;

                // Determine whether this is the *first* successful compile-from-files.
                // Built*Version is -1 until we successfully swapped a file-built program.
                var isFirstFileCompile = (e.BuiltVertVersion < 0 || e.BuiltFragVersion < 0);

                // On the first file compile: only print the "compiling from files" line.
                if (isFirstFileCompile)
                {
                    if (!e.FileLoadedReported)
                    {
                        e.FileLoadedReported = true;
                        Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} compiling from files for '{e.Key}'");
                    }
                }
                else
                {
                    // On subsequent rebuilds: only print hot-reload.
                    Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} hot-reload compile for '{e.Key}'");
                }

                TrySwapProgram(gl, consumer, e, vs, fs, vVer, fVer, isDefault: false);
            }

            foreach (var kv in dict)
            {
                var e = kv.Value;
                while (e.DeleteQueue.TryDequeue(out var p))
                    if (p != 0)
                        gl.DeleteProgram(p);
            }
        }
    }

    private static void TrySwapProgram(
        GL gl,
        IShaderConsumer consumer,
        ProgramEntry e,
        string vs,
        string fs,
        int builtVertVersion,
        int builtFragVersion,
        bool isDefault)
    {
        uint newProg = 0;
        try
        {
            var label = isDefault ? $"{consumer.DebugName}:{e.Key}:DEFAULT" : $"{consumer.DebugName}:{e.Key}";
            newProg = ShaderCompiler.CreateProgram(gl, vs, fs, label: label);
            var old = e.Program;

            e.Program = newProg;

            if (!isDefault)
            {
                e.BuiltVertVersion = builtVertVersion;
                e.BuiltFragVersion = builtFragVersion;
                e.DefaultLoadedReported = false; // if we had default earlier, allow default log again next time we fall back
            }

            e.LastError = null;

            if (old != 0)
                e.DeleteQueue.Enqueue(old);
        }
        catch (Exception ex)
        {
            if (newProg != 0) gl.DeleteProgram(newProg);
            e.LastError = ex.ToString();
        }
    }

    public static void DisposeConsumer(GL gl, IShaderConsumer consumer)
    {
        if (!_byConsumer.TryRemove(consumer, out var dict)) return;

        var lockObj = _consumerLocks.GetOrAdd(consumer, _ => new object());
        lock (lockObj)
        {
            foreach (var kv in dict)
            {
                var e = kv.Value;

                while (e.DeleteQueue.TryDequeue(out var p))
                    if (p != 0) gl.DeleteProgram(p);

                if (e.Program != 0) gl.DeleteProgram(e.Program);
                e.Program = 0;

                e.VertFile.Dispose();
                e.FragFile.Dispose();
            }
        }

        _consumerLocks.TryRemove(consumer, out _);
    }
}
