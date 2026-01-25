namespace MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;

public sealed class CloseEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}