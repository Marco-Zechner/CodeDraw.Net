namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class EngineShaderPaths
{
    private static string _cachedEngineShaderRoot = "";

    public static string ResolveEngineShaderRoot(string folderName = "resources/shaders")
    {
        folderName = folderName.Replace('/', Path.DirectorySeparatorChar);

        if (_cachedEngineShaderRoot.EndsWith(folderName) && Directory.Exists(_cachedEngineShaderRoot))
            return _cachedEngineShaderRoot;


        // 1) Runtime / copied content: next to exe/dll
        var baseDir = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(baseDir, folderName);
        if (Directory.Exists(candidate1) && Directory.GetFiles(candidate1).Length != 0)
        {
            Console.WriteLine("[Info] EngineShaderPaths: Using shader folder: " + candidate1);
            _cachedEngineShaderRoot = candidate1;
            return _cachedEngineShaderRoot;
        }

        // 2) Assembly dir (sometimes different, but usually same as BaseDirectory)
        var asm = typeof(EngineShaderPaths).Assembly;
        var loc = asm.Location;
        if (!string.IsNullOrWhiteSpace(loc))
        {
            var dllDir = Path.GetDirectoryName(Path.GetFullPath(loc))!;
            var candidate2 = Path.Combine(dllDir, folderName);
            if (Directory.Exists(candidate2) && Directory.GetFiles(candidate2).Length != 0)
            {
                Console.WriteLine("[Info] EngineShaderPaths: Using shader folder: " + candidate2);
                _cachedEngineShaderRoot = candidate2;
                return _cachedEngineShaderRoot;
            }
        }

        // 3) Dev fallback: climb upwards from BaseDirectory and search for folderName
        var found = TryFindUpwards(baseDir, folderName, maxDepth: 8);
        if (found != null)
        {
            Console.WriteLine("[Info] EngineShaderPaths: Using shader folder: " + found);
            _cachedEngineShaderRoot = found;
            return _cachedEngineShaderRoot;
        }
        // 4) If nothing found, return the "runtime" path for a clear error later
        Console.WriteLine("[Warning] EngineShaderPaths: Could not find shader folder. Tried:");
        Console.WriteLine("  - " + candidate1);
        if (!string.IsNullOrWhiteSpace(loc))
            Console.WriteLine("  - " + Path.Combine(Path.GetDirectoryName(Path.GetFullPath(loc))!, folderName));
        Console.WriteLine("  - upwards search from: " + baseDir);

        _cachedEngineShaderRoot = candidate1;
        return _cachedEngineShaderRoot;
    }

    private static string? TryFindUpwards(string startDir, string relative, int maxDepth)
    {
        var dir = new DirectoryInfo(startDir);
        for (int i = 0; i < maxDepth && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }
        return null;
    }
}