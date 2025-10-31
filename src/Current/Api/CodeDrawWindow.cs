using MarcoZechner.CodeDrawDotNet.Renderers;

namespace MarcoZechner.CodeDrawDotNet;

public class CodeDrawWindow(string title) : CodeDrawWindow<DefaultWindowRenderer>(title) {}
