using System.Collections.Concurrent;
using System.Diagnostics;

namespace MarcoZechner.CodeDrawDotNet.Shaders;

public static class FileCache
{
    internal sealed class FileEntry
    {
        public string PrimaryAbs = "";
        public string? CsprojFallbackAbs;

        public int RefCount;
        public int HotReloadRefCount;

        public string ActiveAbs = "";  // current chosen source (primary or fallback or "<default>")
        public string Text = "";
        public int Version;
        public string? LastError;

        // warn order + once-per-transition flags
        public bool PrimaryDirMissingWarned;
        public bool FallbackDirMissingWarned;

        public bool PrimaryFileMissingWarned;
        public bool FallbackFileMissingWarned;

        // load reporting (once per source switch)
        public string? LastReportedLoadedFrom; // "primary:...", "fallback:...", "default"

        // probe timing
        public long LastProbeTicks;

        public readonly object Lock = new();
    }

    private sealed class DirWatcher
    {
        public readonly string Dir;
        public readonly FileSystemWatcher Watcher;

        public readonly ConcurrentDictionary<string, byte> WatchedFiles = new(StringComparer.OrdinalIgnoreCase);

        public DirWatcher(string dir)
        {
            Dir = dir;

            Watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            FileSystemEventHandler h = (_, ev) => OnAny(ev.FullPath);
            RenamedEventHandler r = (_, ev) => { OnAny(ev.OldFullPath); OnAny(ev.FullPath); };

            Watcher.Changed += h;
            Watcher.Created += h;
            Watcher.Deleted += h;
            Watcher.Renamed += r;
        }

        private void OnAny(string fullPath)
        {
            var abs = Normalize(fullPath);
            if (!WatchedFiles.ContainsKey(abs)) return;

            // If the active text changes, TryReload() will also emit "hot-reloaded from ..."
            TryReload(abs, fromWatcher: true);
        }
    }

    private static readonly ConcurrentDictionary<string, FileEntry> _files =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, DirWatcher> _dirs =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly object _dirLock = new();

    public sealed class Handle : IDisposable
    {
        private FileEntry? _e;
        private readonly bool _hotReload;

        internal Handle(FileEntry e, bool hotReload)
        {
            _e = e;
            _hotReload = hotReload;
        }

        public string PrimaryAbsPath => _e?.PrimaryAbs ?? "";
        public bool IsValid => _e != null;

        public (string text, int version, string? lastError) Snapshot()
        {
            var e = _e;
            if (e == null) return ("", 0, "disposed");

            lock (e.Lock)
            {
                MaybeProbeLocked(e);
                return (e.Text, e.Version, e.LastError);
            }
        }

        public void Dispose()
        {
            var e = Interlocked.Exchange(ref _e, null);
            if (e == null) return;
            Release(e, _hotReload);
        }
    }

    public static Handle Acquire(string absPath, bool hotReload)
    {
        absPath = Normalize(absPath);

        var e = _files.GetOrAdd(absPath, p =>
        {
            var fe = new FileEntry { PrimaryAbs = p };

            fe.CsprojFallbackAbs = ShaderPath.TryGetCsprojFallbackBase(RemoveExtensionIfPresent(p)) is { } fbBase
                ? AddSameExtension(p, fbBase)
                : null;

            fe.ActiveAbs = p;
            return fe;

            static string RemoveExtensionIfPresent(string path)
            {
                var ext = Path.GetExtension(path);
                return string.IsNullOrWhiteSpace(ext) ? path : path.Substring(0, path.Length - ext.Length);
            }

            static string AddSameExtension(string originalAbs, string newBaseAbs)
            {
                var ext = Path.GetExtension(originalAbs);
                return newBaseAbs + ext;
            }
        });

        lock (e.Lock)
        {
            e.RefCount++;
            if (hotReload) e.HotReloadRefCount++;

            if (e.RefCount == 1)
            {
                // INITIAL: resolve/load (and print "loaded from ..." once)
                ResolveAndLoadLocked(e, forceProbe: true, hotReloadEvent: false);
            }

            if (hotReload)
                EnsureWatchedLocked(e);
        }

        return new Handle(e, hotReload);
    }

    private static void Release(FileEntry e, bool hotReload)
    {
        lock (e.Lock)
        {
            e.RefCount--;
            if (e.RefCount < 0) e.RefCount = 0;

            if (hotReload)
            {
                e.HotReloadRefCount--;
                if (e.HotReloadRefCount < 0) e.HotReloadRefCount = 0;
            }

            if (e.RefCount == 0)
            {
                UnwatchLocked(e.PrimaryAbs);
                if (e.CsprojFallbackAbs != null) UnwatchLocked(e.CsprojFallbackAbs);
            }
        }

        if (Volatile.Read(ref e.RefCount) == 0)
            _files.TryRemove(e.PrimaryAbs, out _);
    }

    public static bool TryReload(string absPath, bool fromWatcher)
    {
        absPath = Normalize(absPath);
        if (!_files.TryGetValue(absPath, out var e)) return false;

        lock (e.Lock)
        {
            // Re-resolve; if it changes active source or updates text, it will print:
            // - "loaded from ..." when source switches
            // - "hot-reloaded from ..." when same source updates content
            return ResolveAndLoadLocked(e, forceProbe: true, hotReloadEvent: fromWatcher);
        }
    }

    // Backwards-compatible signature used by DirWatcher:
    public static bool TryReload(string absPath) => TryReload(absPath, fromWatcher: true);

    private static void MaybeProbeLocked(FileEntry e)
    {
        // Probe at most every 0.5s
        var now = Stopwatch.GetTimestamp();
        var minDelta = Stopwatch.Frequency / 2;
        if (now - e.LastProbeTicks < minDelta) return;

        e.LastProbeTicks = now;

        // Probe should NOT spam hot-reload logs; treat as not-from-watcher.
        ResolveAndLoadLocked(e, forceProbe: false, hotReloadEvent: false);
    }

    private static bool ResolveAndLoadLocked(FileEntry e, bool forceProbe, bool hotReloadEvent)
    {
        // ---- 0) Directory warnings FIRST (your requirement) ----
        // Primary dir
        WarnIfDirMissingOncePerTransition(
            absFile: e.PrimaryAbs,
            ref e.PrimaryDirMissingWarned,
            keyPrefix: "DirMissing",
            msgPrefix: "[Warning] FileCache: watch directory missing"
        );

        // Fallback dir
        if (e.CsprojFallbackAbs != null)
        {
            WarnIfDirMissingOncePerTransition(
                absFile: e.CsprojFallbackAbs,
                ref e.FallbackDirMissingWarned,
                keyPrefix: "DirMissing",
                msgPrefix: "[Warning] FileCache: watch directory missing"
            );
        }

        // ---- 1) Try primary (highest priority) ----
        if (TryReadFile(e.PrimaryAbs, out var txtPrimary))
        {
            // Recover flags so a later disappearance can warn again.
            e.PrimaryFileMissingWarned = false;

            var changed = PublishIfChanged(
                e,
                newActiveAbs: e.PrimaryAbs,
                newText: txtPrimary,
                newError: null,
                hotReloadEvent: hotReloadEvent,
                sourceTag: "primary"
            );
            return changed;
        }

        // Primary file missing warning (after dir warning)
        WarnIfFileMissingOncePerTransition(
            absFile: e.PrimaryAbs,
            ref e.PrimaryFileMissingWarned
        );

        // ---- 2) Try fallback (csproj) ----
        if (e.CsprojFallbackAbs != null && TryReadFile(e.CsprojFallbackAbs, out var txtFallback))
        {
            e.FallbackFileMissingWarned = false;

            // Only go to fallback because primary is not readable right now.
            var changed = PublishIfChanged(
                e,
                newActiveAbs: e.CsprojFallbackAbs,
                newText: txtFallback,
                newError: null,
                hotReloadEvent: hotReloadEvent,
                sourceTag: "csproj"
            );
            return changed;
        }

        if (e.CsprojFallbackAbs != null)
            WarnIfFileMissingOncePerTransition(
                absFile: e.CsprojFallbackAbs,
                ref e.FallbackFileMissingWarned
            );

        // ---- 3) None found/readable: keep old text (no flicker) but set error ----
        e.LastError = "File not found (primary+fallback)";
        return false;
    }

    private static bool PublishIfChanged(
        FileEntry e,
        string newActiveAbs,
        string newText,
        string? newError,
        bool hotReloadEvent,
        string sourceTag
    )
    {
        var sourceSwitch = !string.Equals(e.ActiveAbs, newActiveAbs, StringComparison.OrdinalIgnoreCase);
        var textChanged = !string.Equals(e.Text, newText, StringComparison.Ordinal);

        // Always clear error on successful read
        e.LastError = newError;

        if (!sourceSwitch && !textChanged)
            return false;

        e.ActiveAbs = newActiveAbs;
        e.Text = newText;
        e.Version++;

        // Reporting:
        // - If source switched: "loaded from ..." ONCE per switch
        // - If same source and text changed:
        //   - if came from watcher: "hot-reloaded from ..." (once per change, which is fine)
        //   - otherwise (probe/manual): stay quiet (no spam)
        if (sourceSwitch)
        {
            ReportLoadedFromOnce(e, sourceTag, newActiveAbs);
        }
        else if (hotReloadEvent && textChanged)
        {
            Console.WriteLine($"[Info] FileCache: hot-reloaded from {sourceTag}: {newActiveAbs}");
        }

        return true;
    }

    private static void ReportLoadedFromOnce(FileEntry e, string sourceTag, string abs)
    {
        var token = $"{sourceTag}:{abs}";
        if (string.Equals(e.LastReportedLoadedFrom, token, StringComparison.OrdinalIgnoreCase))
            return;

        e.LastReportedLoadedFrom = token;
        Console.WriteLine($"[Info] FileCache: loaded from {sourceTag}: {abs}");
    }

    private static void WarnIfDirMissingOncePerTransition(string absFile, ref bool warnedFlag, string keyPrefix, string msgPrefix)
    {
        var dir = Path.GetDirectoryName(absFile);
        if (string.IsNullOrWhiteSpace(dir)) return;

        var exists = Directory.Exists(dir);
        if (exists)
        {
            // recovered -> allow warn again next time it disappears
            warnedFlag = false;
            return;
        }

        if (warnedFlag) return;
        warnedFlag = true;

        // This key prevents spam across multiple shaders in same missing directory
        ShaderPath.WarnOnce(
            key: $"{keyPrefix}:{dir}",
            message: $"{msgPrefix}: {dir} (hot reload disabled for that folder until it exists)."
        );
    }

    private static void WarnIfFileMissingOncePerTransition(string absFile, ref bool warnedFlag)
    {
        if (File.Exists(absFile))
        {
            warnedFlag = false; // recovered -> allow warn again later
            return;
        }

        if (warnedFlag) return;
        warnedFlag = true;

        ShaderPath.WarnOnce(
            key: $"FileMissing:{absFile}",
            message: $"[Warning] FileCache: shader file missing: {absFile} -> trying CsProject fallback."
        );
    }

    private static bool TryReadFile(string absPath, out string text)
    {
        text = "";
        try
        {
            if (!File.Exists(absPath)) return false;
            text = File.ReadAllText(absPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureWatchedLocked(FileEntry e)
    {
        EnsureDirWatchForFileLocked(e.PrimaryAbs);
        if (e.CsprojFallbackAbs != null)
            EnsureDirWatchForFileLocked(e.CsprojFallbackAbs);
    }

    private static void EnsureDirWatchForFileLocked(string absFile)
    {
        var dir = Path.GetDirectoryName(absFile);
        if (string.IsNullOrWhiteSpace(dir)) return;

        // If directory doesn't exist: don't create watcher (also avoids exceptions).
        if (!Directory.Exists(dir))
        {
            // Warning is handled by ResolveAndLoadLocked's dir warning (and "once")
            return;
        }

        var dw = _dirs.GetOrAdd(dir, d =>
        {
            lock (_dirLock)
            {
                if (_dirs.TryGetValue(d, out var existing)) return existing;
                return new DirWatcher(d);
            }
        });

        dw.WatchedFiles[absFile] = 1;
    }

    private static void UnwatchLocked(string absPath)
    {
        absPath = Normalize(absPath);
        var dir = Path.GetDirectoryName(absPath);
        if (string.IsNullOrWhiteSpace(dir)) return;

        if (_dirs.TryGetValue(dir, out var dw))
        {
            dw.WatchedFiles.TryRemove(absPath, out _);

            if (dw.WatchedFiles.IsEmpty)
            {
                lock (_dirLock)
                {
                    if (dw.WatchedFiles.IsEmpty && _dirs.TryRemove(dir, out var removed))
                        removed.Watcher.Dispose();
                }
            }
        }
    }

    private static string Normalize(string p) => Path.GetFullPath(p);
}
