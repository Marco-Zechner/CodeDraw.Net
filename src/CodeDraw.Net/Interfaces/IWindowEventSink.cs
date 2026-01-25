using MarcoZechner.CodeDrawDotNet.Interfaces.Primitives;
using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface IWindowEventSink
{
    void RaiseWindowSize(int w, int h);
    void RaiseFramebufferSize(int w, int h);
    void RaiseKey(Keys k, int sc, InputAction a, KeyModifiers m);
    void RaiseMouseButton(MouseButton b, InputAction a, KeyModifiers m);
    void RaiseCursorPos(double x, double y);
    void RaiseScroll(double xoff, double yoff);
    void RaiseFileDropped(string[] paths);
    void RaiseRefresh();
    void RaiseFocus(bool focused);
    void RaiseIconify(bool iconified);
    void RaiseMaximize(bool maximized);

    // close flow entry from UI thread
    void OnNativeCloseRequestedFromUI(CloseReason reason = CloseReason.USER_CLOSED_WINDOW);
}