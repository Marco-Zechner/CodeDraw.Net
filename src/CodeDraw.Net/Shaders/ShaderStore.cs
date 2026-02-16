using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Shaders;

public static class ShaderStore
{
    private sealed class ProgramEntry
    {
        public ShaderKey Key;

        public FileCache.Handle VertFile = null!;
        public FileCache.Handle FragFile = null!;

        public uint Program;

        // Versions of the currently active *file-built* program.
        // -1 means: no successful file-built program yet.
        public int BuiltVertVersion = -1;
        public int BuiltFragVersion = -1;

        public string? LastError;
        public string? LastErrorReported;

        // When a file compile fails, we keep showing default until files change again.
        public bool ForceDefaultUntilFileChange;
        public int ForceDefaultVertVersion = -1;
        public int ForceDefaultFragVersion = -1;

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
            BuiltFragVersion = -1,
            ForceDefaultUntilFileChange = false,
            ForceDefaultVertVersion = -1,
            ForceDefaultFragVersion = -1,
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

                // If we were forced to use default until files change, clear that flag
                // as soon as the versions differ from the ones that triggered the fail.
                if (e.ForceDefaultUntilFileChange)
                {
                    if (vVer != e.ForceDefaultVertVersion || fVer != e.ForceDefaultFragVersion)
                    {
                        e.ForceDefaultUntilFileChange = false;
                        e.DefaultLoadedReported = false; // allow "loaded default ..." again if needed later
                        // We purposely do NOT clear FileLoadedReported; it's still the same shader "family".
                        // We also clear LastErrorReported so a new compile failure can print again.
                        e.LastErrorReported = null;
                    }
                }

                var filesBad = (vErr != null || fErr != null);

                // ---- A) File I/O errors => fallback to default (but keep last good if any) ----
                if (filesBad)
                {
                    e.LastError = $"File error: vert='{vErr ?? "ok"}', frag='{fErr ?? "ok"}'";

                    // If we have any program already (file-built or default), keep it.
                    // (Hot reload is effectively paused by file errors.)
                    if (e.Program != 0)
                        continue;

                    if (DefaultShaderSources.TryGet(e.Key, out var dvs, out var dfs))
                    {
                        if (!e.DefaultLoadedReported)
                        {
                            e.DefaultLoadedReported = true;
                            Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} loaded default for '{e.Key}' due to file error\n{e.LastError}");
                        }

                        TrySwapProgram(gl, consumer, e, dvs, dfs, builtVertVersion: vVer, builtFragVersion: fVer, isDefault: true);
                    }

                    continue;
                }

                // ---- B) No file I/O errors ----

                // If we're in "force default until file change" mode and files haven't changed,
                // do NOTHING: keep default, do not attempt compile again.
                if (e.ForceDefaultUntilFileChange &&
                    vVer == e.ForceDefaultVertVersion &&
                    fVer == e.ForceDefaultFragVersion)
                {
                    continue;
                }

                // If nothing changed since last successful file-built program, skip.
                if (vVer == e.BuiltVertVersion && fVer == e.BuiltFragVersion)
                    continue;

                // Determine whether this is the *first* successful compile-from-files.
                var isFirstFileCompile = (e.BuiltVertVersion < 0 || e.BuiltFragVersion < 0);

                // On first file compile: only print "compiling from files".
                // On subsequent rebuilds: only print "hot-reload compile".
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
                    Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} hot-reload compile for '{e.Key}'");
                }

                // Try compile from files
                var swapped = TrySwapProgram(gl, consumer, e, vs, fs, vVer, fVer, isDefault: false);

                // If compile failed: immediately fallback to default and STAY there until files change.
                if (!swapped)
                {
                    e.ForceDefaultUntilFileChange = true;
                    e.ForceDefaultVertVersion = vVer;
                    e.ForceDefaultFragVersion = fVer;

                    if (DefaultShaderSources.TryGet(e.Key, out var dvs, out var dfs))
                    {
                        if (!e.DefaultLoadedReported)
                        {
                            e.DefaultLoadedReported = true;
                            Console.WriteLine($"[Info] ShaderStore:{consumer.DebugName} loaded default for '{e.Key}' due to compile/link error");
                        }

                        // Important: treat as default, do NOT change Built*Version.
                        TrySwapProgram(gl, consumer, e, dvs, dfs, builtVertVersion: vVer, builtFragVersion: fVer, isDefault: true);
                    }
                }
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

    // Returns true if it successfully swapped in the new program.
    private static bool TrySwapProgram(
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
                e.ForceDefaultUntilFileChange = false; // file-built success wins
            }

            e.LastError = null;
            e.LastErrorReported = null;

            if (old != 0)
                e.DeleteQueue.Enqueue(old);

            return true;
        }
        catch (Exception ex)
        {
            if (newProg != 0) gl.DeleteProgram(newProg);

            var msg = ex.ToString();
            e.LastError = msg;

            if (!string.Equals(e.LastErrorReported, msg, StringComparison.Ordinal))
            {
                e.LastErrorReported = msg;
                Console.WriteLine($"[Error] ShaderStore:{consumer.DebugName} compile/link failed for {e.Key}\n{msg}");
            }

            return false;
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
