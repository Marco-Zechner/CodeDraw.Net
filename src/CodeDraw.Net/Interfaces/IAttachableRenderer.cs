namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface IAttachableRenderer {
    void Attach(IWindowHost host, nint native, string title, IRenderThreadCallbacks cb, IWindowSettings settings);
    void Start();
    void StopAndJoin();
    long SealFrame();
    void WaitForPresented(long? token = null);
    void WaitForInflightSlot();
    void Enqueue(IRenderAction action);
    int MaxInflightFrames { get; set; }
    // Optional metrics:
    int BacklogFrames { get; }
    int QueuedFrames  { get; }
    int InflightFrames{ get; }
    long Frames       { get; }
    double Fps        { get; }
    TimeSpan Uptime { get; }
    
    bool IsRenderThread();
}
