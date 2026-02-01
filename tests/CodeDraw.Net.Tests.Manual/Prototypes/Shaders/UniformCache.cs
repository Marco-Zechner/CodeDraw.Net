using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public sealed class UniformCache
{
    // Program -> (name -> location)
    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<string, int>> _cache = new();

    /// <summary>Get uniform location with per-program caching.</summary>
    public int Get(GL gl, uint program, string name)
    {
        if (program == 0) return -1;

        var dict = _cache.GetOrAdd(program, _ => new ConcurrentDictionary<string, int>(StringComparer.Ordinal));
        if (dict.TryGetValue(name, out var loc)) return loc;

        loc = gl.GetUniformLocation(program, name);
        dict[name] = loc;
        return loc;
    }

    /// <summary>Call when you delete a program to keep the cache tidy.</summary>
    public void Invalidate(uint program)
    {
        if (program == 0) return;
        _cache.TryRemove(program, out _);
    }

    public void Clear() => _cache.Clear();
}