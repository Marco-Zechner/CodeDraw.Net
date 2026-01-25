using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

// Per-window GL resources, keyed by native window handle (nint).
// This lets IRenderAction look up the shader program without changing your public API.
internal sealed class Gl2DResources : IDisposable
{
    private static readonly ConcurrentDictionary<nint, Gl2DResources> _map = new();

    public static Gl2DResources Get(nint window)
        => _map.TryGetValue(window, out var r)
            ? r
            : throw new InvalidOperationException($"Gl2DResources not installed for window {window}.");

    public static void Install(GL gl, nint window)
    {
        // Replace if reinstalled (shouldn't happen in normal flow, but safe).
        if (_map.TryRemove(window, out var old))
            old.DisposeWith(gl);

        var res = new Gl2DResources(gl);
        _map[window] = res;
    }

    public static void Uninstall(GL gl, nint window)
    {
        if (_map.TryRemove(window, out var res))
            res.DisposeWith(gl);
    }

    // ---- resources you need for 2D drawing ----
    public uint Program2D { get; private set; }
    public int LocViewport { get; private set; }

    private Gl2DResources(GL gl)
    {
        Program2D = ShaderUtils.CreateProgram(gl, Shaders2D.VERTEX, Shaders2D.FRAGMENT);
        LocViewport = gl.GetUniformLocation(Program2D, "uViewport");
        if (LocViewport < 0)
            throw new Exception("2D shader missing uniform uViewport (or optimized out).");

        // Good defaults for 2D with alpha:
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);

        // You already enable sRGB in your renderer; keep it there.
    }

    public void Dispose()
    {
        // no-op here (we need a GL instance to delete resources)
    }

    private void DisposeWith(GL gl)
    {
        if (Program2D != 0)
        {
            gl.DeleteProgram(Program2D);
            Program2D = 0;
        }
    }
}