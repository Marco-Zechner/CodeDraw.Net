namespace MarcoZechner.CodeDrawDotNet.Api.Events;

public sealed class CloseEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}