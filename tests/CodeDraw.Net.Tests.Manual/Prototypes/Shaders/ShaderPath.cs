using System.Collections.Concurrent;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class ShaderPath
{
    public static bool ForceEngineToProject = true;
    public static bool ForceAppToProject = false;

    private static string? _engineRoot;
    private static string? _appRoot;
    private static string? _csprojRoot;
    private static string? _gitRoot;

    private static readonly Lock _lock = new();

    // warn-once keys (folder missing, file missing, etc.)
    private static readonly ConcurrentDictionary<string, byte> _warned = new(StringComparer.OrdinalIgnoreCase);

    public static string Engine(string name, string folder = "resources/shaders")
    {
        if (ForceEngineToProject)
        {
            WarnOnce("ForceEngineToProject",
                "ShaderPath.Engine: Forced to use CsProject path due to ForceEngineToProject=true");
            return CsProject(name, folder);
        }

        var root = GetEngineRoot(folder);
        return Path.Combine(root, NormalizeName(name));
    }

    public static string App(string name, string folder = "resources/shaders")
    {
        if (ForceAppToProject)
        {
            WarnOnce("ForceAppToProject",
                "ShaderPath.App: Forced to use CsProject path due to ForceAppToProject=true");
            return CsProject(name, folder);
        }

        var root = GetAppRoot(folder);
        return Path.Combine(root, NormalizeName(name));
    }

    public static string CsProject(string name, string folder = "resources/shaders")
    {
        var root = GetCsprojRoot();
        return Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar), NormalizeName(name));
    }

    public static string GitRoot(string name, string folder = "resources/shaders")
    {
        var root = GetGitRoot();
        return Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar), NormalizeName(name));
    }

    /// <summary>
    /// Given an absolute shader *base* path (without extension), tries to compute a csproj fallback base path
    /// that mirrors the tail after ".../resources/shaders".
    /// Example: "X:/bin/.../resources/shaders/shaderTest/rect" -> "{csproj}/resources/shaders/shaderTest/rect".
    /// Returns null if the marker segment can't be found.
    /// </summary>
    public static string? TryGetCsprojFallbackBase(string requestedBaseAbs, string marker = "resources/shaders")
    {
        try
        {
            requestedBaseAbs = Path.GetFullPath(requestedBaseAbs);

            var markerNorm = marker.Replace('/', Path.DirectorySeparatorChar);
            var idx = requestedBaseAbs.IndexOf(markerNorm, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var tail = requestedBaseAbs.Substring(idx + markerNorm.Length);
            tail = tail.TrimStart(Path.DirectorySeparatorChar);

            var csproj = GetCsprojRoot();
            var fallback = Path.Combine(csproj, markerNorm, tail);
            return Path.GetFullPath(fallback);
        }
        catch
        {
            return null;
        }
    }

    public static void WarnOnce(string key, string message)
    {
        if (_warned.TryAdd(key, 1))
            Console.WriteLine(message);
    }

    private static string GetEngineRoot(string folder)
    {
        lock (_lock)
        {
            if (_engineRoot != null) return _engineRoot;

            folder = folder.Replace('/', Path.DirectorySeparatorChar);
            var asm = typeof(ShaderPath).Assembly;
            var loc = asm.Location;

            var baseDir = AppContext.BaseDirectory;
            var candidate1 = Path.Combine(baseDir, folder);
            if (Directory.Exists(candidate1) && Directory.GetFiles(candidate1).Length != 0)
                return _engineRoot = Path.GetFullPath(candidate1);

            if (!string.IsNullOrWhiteSpace(loc))
            {
                var dllDir = Path.GetDirectoryName(Path.GetFullPath(loc))!;
                var candidate2 = Path.Combine(dllDir, folder);
                if (Directory.Exists(candidate2) && Directory.GetFiles(candidate2).Length != 0)
                    return _engineRoot = Path.GetFullPath(candidate2);
            }

            // no crash; just return runtime path (FileCache will handle missing)
            return _engineRoot = Path.GetFullPath(candidate1);
        }
    }

    private static string GetAppRoot(string folder)
    {
        lock (_lock)
        {
            if (_appRoot != null) return _appRoot;
            folder = folder.Replace('/', Path.DirectorySeparatorChar);
            var root = Path.Combine(AppContext.BaseDirectory, folder);
            return _appRoot = Path.GetFullPath(root);
        }
    }

    private static string GetCsprojRoot()
    {
        lock (_lock)
        {
            if (_csprojRoot != null) return _csprojRoot;

            var cur = new DirectoryInfo(Environment.CurrentDirectory);
            for (var i = 0; i < 12 && cur != null; i++)
            {
                if (cur.GetFiles("*.csproj").Length > 0)
                    return _csprojRoot = cur.FullName;
                cur = cur.Parent;
            }

            return _csprojRoot = Environment.CurrentDirectory;
        }
    }

    private static string GetGitRoot()
    {
        lock (_lock)
        {
            if (_gitRoot != null) return _gitRoot;

            var cur = new DirectoryInfo(Environment.CurrentDirectory);
            for (var i = 0; i < 20 && cur != null; i++)
            {
                var git = Path.Combine(cur.FullName, ".git");
                if (Directory.Exists(git))
                    return _gitRoot = cur.FullName;
                cur = cur.Parent;
            }

            return _gitRoot = Environment.CurrentDirectory;
        }
    }

    private static string NormalizeName(string name)
        => name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}
