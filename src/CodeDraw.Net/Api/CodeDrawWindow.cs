
using MarcoZechner.CodeDrawDotNet.Renderers.Default;

namespace MarcoZechner.CodeDrawDotNet.Api;

public class CodeDrawWindow(string title) : CodeDrawWindow<DefaultWindowRenderer>(title) {}
