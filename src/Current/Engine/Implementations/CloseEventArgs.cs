namespace MarcoZechner.CodeDrawDotNet.Engine;

public sealed class CloseEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}