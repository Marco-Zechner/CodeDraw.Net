namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class AppShaderPaths
{
    private static string _cachedAppShaderRoot = "";

    public static string ResolveAppShaderRoot(string folderName = "resources/shaders")
    {
        folderName = folderName.Replace('/', Path.DirectorySeparatorChar);

        if (_cachedAppShaderRoot.EndsWith(folderName) && Directory.Exists(_cachedAppShaderRoot))
            return _cachedAppShaderRoot;

        // 1) Prefer bin output folder
        var bin = Path.Combine(AppContext.BaseDirectory, folderName);
        if (Directory.Exists(bin))
        {
            Console.WriteLine("[Info] AppShaderPaths: Using shader folder: " + bin);
            _cachedAppShaderRoot = bin;
            return _cachedAppShaderRoot;
        }

        // 2) Dev fallback: search up from current working directory
        var cur = new DirectoryInfo(Environment.CurrentDirectory);
        for (var i = 0; i < 10 && cur != null; i++)
        {
            var candidate = Path.Combine(cur.FullName, folderName);
            if (Directory.Exists(candidate))
            {
                Console.WriteLine("[Info] AppShaderPaths: Using shader folder: " + candidate);
                _cachedAppShaderRoot = candidate;
                return _cachedAppShaderRoot;
            }

            cur = cur.Parent;
        }

        // 3) Last resort: return bin path anyway
        Console.WriteLine("[Info] AppShaderPaths: Using shader folder: " + bin);
        _cachedAppShaderRoot = bin;
        return _cachedAppShaderRoot;
    }
}