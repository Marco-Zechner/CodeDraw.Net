using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public sealed class AutoUniform(GL gl, ShaderStore store, AutoProgram prog, string name)
{
    private uint _cachedProgram;
    private int _cachedLoc;
    private bool _hasValue;

    public int Location
    {
        get
        {
            var p = prog.Handle;
            if (p == 0) return -1;

            if (_hasValue && _cachedProgram == p) return _cachedLoc;

            var loc = store.GetUniformLocation(gl, p, name);
            _cachedProgram = p;
            _cachedLoc = loc;
            _hasValue = true;
            return loc;
        }
    }

    public static implicit operator int(AutoUniform u) => u.Location;
}
