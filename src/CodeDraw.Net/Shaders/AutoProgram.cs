namespace MarcoZechner.CodeDrawDotNet.Shaders;

/// <summary>
/// Pure getter: returns the currently compiled program handle for this consumer+key.
/// Compilation happens only in ShaderStore.CheckHotReload(gl, consumer).
/// </summary>
public sealed class AutoProgram
{
    private readonly IShaderConsumer _consumer;
    private readonly ShaderKey _key;

    private uint _cached;

    public AutoProgram(IShaderConsumer consumer, string vertBaseAbs)
        : this(consumer, new ShaderKey(Abs(vertBaseAbs), Abs(vertBaseAbs)))
    { }

    public AutoProgram(IShaderConsumer consumer, string vertBaseAbs, string fragBaseAbs)
        : this(consumer, new ShaderKey(Abs(vertBaseAbs), Abs(fragBaseAbs)))
    { }

    public AutoProgram(IShaderConsumer consumer, ShaderKey key)
    {
        _consumer = consumer;
        _key = key;

        ShaderStore.Register(_consumer, _key); // ensures files are acquired & tracked
    }

    public uint Handle
    {
        get
        {
            var p = ShaderStore.GetProgram(_consumer, _key);
            _cached = p;
            return p;
        }
    }

    public override string ToString() => $"{_key}@0x{_cached:X}";
    public static implicit operator uint(AutoProgram p) => p.Handle;

    private static string Abs(string p) => Path.GetFullPath(p);
}