using MarcoZechner.CodeDrawDotNet.Interfaces;

namespace MarcoZechner.CodeDrawDotNet.Api;

public delegate void CloseRequestedHandler(CodeDrawWindowBase window, CloseEventArgs args, CloseReason reason);
