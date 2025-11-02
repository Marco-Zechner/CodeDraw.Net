namespace MarcoZechner.CodeDrawDotNet.Api;

public interface IHostBootstrap<TSelf> where TSelf : IHostBootstrap<TSelf>
{
    static abstract void EnsureHost(); // called once, must init CodeDrawRuntime with an IWindowHost
}
