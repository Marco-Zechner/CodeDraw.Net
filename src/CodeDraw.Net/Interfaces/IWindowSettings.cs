using MarcoZechner.ColorDotNet;

namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface IWindowSettings {
    bool VSync { get; set; }
    int TargetFps { get; set; }
    int LongActionWarnMs { get; set; }
    int MaxInflightFrames { get; set; }
    int UpdateIntervalMs { get; set; }
    Color ClearColor { get; set; }
}