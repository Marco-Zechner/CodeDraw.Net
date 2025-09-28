using MarcoZechner.Math;
using Silk.NET.Windowing;
using WindowBorderSilk = Silk.NET.Windowing.WindowBorder;
using WindowStateSilk = Silk.NET.Windowing.WindowState;

namespace MarcoZechner.CodeDraw.Net;

public class CodeDrawOptions {
    /// <summary>
    /// The size of the window in pixels.
    /// </summary>
    public Vector2 Size { get; set; } = new Vector2(600, 600);
    /// <summary>
    /// The position of the top left corner for drawing area in pixels.
    /// Title bar and borders are not included.
    /// </summary>
    public Vector2 Position { get; set; } = new Vector2(-1, -1);
    /// <summary>
    /// The title of the window that is displayed in the title bar.
    /// </summary>
    public string Title { get; set; } = "CodeDraw";
    /// <summary>
    /// If the window should be always on top of other windows.
    /// Note: Other windows created later with the same flag will be on top of this window.
    /// </summary>
    public bool IsAlwaysOnTop { get; set; } = false;
    /// <summary>
    /// Controls the Border of the Window (if it is resizable, fixed or hidden).
    /// </summary>
    /// <remarks>
    /// Note: If the border is hidden there will also be no title bar, and no close/minimize button!
    /// </remarks>
    public WindowBorder WindowBorder { get; set; } = WindowBorder.Resizable;
    /// <summary>
    /// Controls the state of the window (normal, minimized, maximized, fullscreen).
    /// </summary>
    /// <remarks>
    /// Note: As of now, this value does not get updated if the user changes the State of the window manually.
    /// </remarks>
    public WindowState WindowState { get; set; } = WindowState.Normal; //TODO: update this value if it gets changed

    // implicit convertion to WindowOptions
    public static implicit operator WindowOptions(CodeDrawOptions options) => 
        WindowOptions.Default with
        {
            Title = options.Title,
            Size = options.Size,
            Position = options.Position,
            TopMost = options.IsAlwaysOnTop,
            WindowBorder = options.WindowBorder switch
            {
                WindowBorder.Resizable => WindowBorderSilk.Resizable,
                WindowBorder.Fixed => WindowBorderSilk.Fixed,
                WindowBorder.Hidden => WindowBorderSilk.Hidden,
                _ => WindowBorderSilk.Resizable,
            },
            WindowState = options.WindowState switch
            {
                WindowState.Normal => WindowStateSilk.Normal,
                WindowState.Minimized => WindowStateSilk.Minimized,
                WindowState.Maximized => WindowStateSilk.Maximized,
                WindowState.Fullscreen => WindowStateSilk.Fullscreen,
                _ => WindowStateSilk.Normal,
            }
        };
}