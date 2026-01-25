
namespace MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;

public enum CloseReason
{
    /// <summary>The window was closed due to an unknown reason.</summary>
    UNKNOWN = 0,

    /// <summary>The window was closed by user code calling <see cref="CodeDrawWindow.RequestClose"/></summary>
    REQUESTED_BY_USER = 1,

    /// <summary>The window was closed by the user clicking the close button (X) or pressing Alt+F4.</summary>
    USER_CLOSED_WINDOW = 2,

    /// <summary>The window was closed because the WaitForClose event returned true, or the user defined key was pressed.</summary>
    WAIT_FOR_CLOSE_EVENT = 3,

    /// <summary>The window was already closed when WaitForClose was called.</summary>
    ALREADY_CLOSED = 4
}