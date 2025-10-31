namespace MarcoZechner.CodeDrawDotNet.Api.Events;

public delegate void CloseRequestedHandler(CodeDrawWindowBase window, CloseEventArgs args, CloseReason reason);
