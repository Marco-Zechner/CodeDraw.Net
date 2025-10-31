using MarcoZechner.CodeDrawDotNet.Renderers;

namespace MarcoZechner.CodeDrawDotNet.Api;

public class CodeDrawWindow(string title) : CodeDrawWindow<DefaultWindowRenderer>(title) {}
