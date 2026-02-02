namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public sealed class AutoProgram(ShaderStore store, string name)
{
    private uint _cached;

    public uint Handle
    {
        get
        {
            var p = store.GetProgram(name);
            _cached = p;
            return p;
        }
    }

    public override string ToString() => $"{name}@0x{_cached:X}";
    public static implicit operator uint(AutoProgram p) => p.Handle;
}