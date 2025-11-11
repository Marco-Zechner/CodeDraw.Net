namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public sealed class CloseEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}