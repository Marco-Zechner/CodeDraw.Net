using System.Collections.Concurrent;

namespace MarcoZechner.CodeDrawDotNet.Images;

public static class ImagePath
{
    public static bool ForceEngineToProject = true;
    public static bool ForceAppToProject = false;

    private static string? _engineRoot;
    private static string? _appRoot;
    private static string? _csprojRoot;
    private static string? _gitRoot;

    private static readonly Lock _lock = new();
    private static readonly ConcurrentDictionary<string, byte> _warned = new(StringComparer.OrdinalIgnoreCase);

    public static string Engine(string name, string folder = "resources/images")
    {
        if (ForceEngineToProject)
        {
            WarnOnce("ForceEngineToProject",
                "ImagePath.Engine: Forced to use CsProject path due to ForceEngineToProject=true");
            return CsProject(name, folder);
        }

        var root = GetEngineRoot(folder);
        return Path.Combine(root, NormalizeName(name));
    }

    public static string App(string name, string folder = "resources/images")
    {
        if (ForceAppToProject)
        {
            WarnOnce("ForceAppToProject",
                "ImagePath.App: Forced to use CsProject path due to ForceAppToProject=true");
            return CsProject(name, folder);
        }

        var root = GetAppRoot(folder);
        return Path.Combine(root, NormalizeName(name));
    }

    public static string CsProject(string name, string folder = "resources/images")
    {
        var root = GetCsprojRoot();
        return Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar), NormalizeName(name));
    }

    public static string GitRoot(string name, string folder = "resources/images")
    {
        var root = GetGitRoot();
        return Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar), NormalizeName(name));
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
            var asm = typeof(ImagePath).Assembly;
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
                if (Directory.Exists(Path.Combine(cur.FullName, ".git")))
                    return _gitRoot = cur.FullName;
                cur = cur.Parent;
            }

            return _gitRoot = Environment.CurrentDirectory;
        }
    }

    private static string NormalizeName(string name)
        => name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
}