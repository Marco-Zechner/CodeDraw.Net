using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

/// <summary>
/// Uniform location cache keyed by program handle.
/// </summary>
public sealed class AutoUniform
{
    private readonly GL _gl;
    private readonly IShaderConsumer _consumer;
    private readonly AutoProgram _prog;
    private readonly string _name;

    private uint _cachedProgram;
    private int _cachedLoc;
    private bool _hasValue;

    public AutoUniform(GL gl, IShaderConsumer consumer, AutoProgram prog, string name)
    {
        _gl = gl;
        _consumer = consumer;
        _prog = prog;
        _name = name;
    }

    public int Location
    {
        get
        {
            var p = _prog.Handle;
            if (p == 0) return -1;
            if (_hasValue && _cachedProgram == p) return _cachedLoc;

            var loc = ShaderStore.GetUniformLocation(_gl, p, _name);
            _cachedProgram = p;
            _cachedLoc = loc;
            _hasValue = true;
            return loc;
        }
    }

    public static implicit operator int(AutoUniform u) => u.Location;
}