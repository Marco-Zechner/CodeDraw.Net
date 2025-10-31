
namespace MarcoZechner.CodeDrawDotNet.Api.Events;

public enum CloseReason
{
    /// <summary>The window was closed due to an unknown reason.</summary>
    Unknown = 0,

    /// <summary>The window was closed by user code calling <see cref="CodeDrawWindow.RequestClose"/></summary>
    RequestedByUser = 1,

    /// <summary>The window was closed by the user clicking the close button (X) or pressing Alt+F4.</summary>
    UserClosedWindow = 2,

    /// <summary>The window was closed because the WaitForClose event returned true, or the user defined key was pressed.</summary>
    WaitForCloseEvent = 3,

    /// <summary>The window was already closed when WaitForClose was called.</summary>
    AlreadyClosed = 4
}