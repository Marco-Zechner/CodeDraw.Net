using MarcoZechner.CodeDrawDotNet.Api.Renderers;

namespace MarcoZechner.CodeDrawDotNet.Api;

public class CodeDrawWindow(string title) : CodeDrawWindow<DefaultWindowRenderer>(title) {}
