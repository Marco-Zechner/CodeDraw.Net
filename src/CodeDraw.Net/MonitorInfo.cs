namespace MarcoZechner.CodeDrawDotNet;

public readonly record struct MonitorInfo(
    nint GlfwHandle,
    string Name,
    int WorkX, int WorkY,
    int WorkWidth, int WorkHeight,
    float ContentScaleX,
    float ContentScaleY,
    int RefreshRate
);