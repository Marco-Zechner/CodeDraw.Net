using MarcoZechner.CodeDrawDotNet.Interfaces;
using MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;

namespace MarcoZechner.CodeDrawDotNet.Api;

public delegate void CloseRequestedHandler(CodeDrawWindowBase window, CloseEventArgs args, CloseReason reason);
