namespace MarcoZechner.CodeDrawDotNet.Api;

public static class CodeDrawRuntime
{
    private static IWindowHost? _host;
    private static int _initialized; // 0 = no, 1 = yes

    public static void Init(IWindowHost host)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (Interlocked.Exchange(ref _initialized, 1) == 1) return; // already set
        _host = host;
    }

    internal static IWindowHost Host =>
        _host ?? throw new InvalidOperationException(
            "CodeDraw runtime not initialized. " +
            "Call CodeDrawHostBootstrap.Install() (from the Engine Impl) before creating windows.");
}
