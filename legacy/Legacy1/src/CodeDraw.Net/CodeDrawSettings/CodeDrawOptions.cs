using Legacy1.MarcoZechner.CodeDrawDotNet.Extensions;
using MarcoZechner.MathDotNet;
using WindowBorder = Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.WindowOptions.WindowBorder;
using WindowBorderSilk = Silk.NET.Windowing.WindowBorder;
using WindowState = Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings.WindowOptions.WindowState;
using WindowStateSilk = Silk.NET.Windowing.WindowState;

namespace Legacy1.MarcoZechner.CodeDrawDotNet.CodeDrawSettings;

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
    public WindowBorder WindowBorder { get; set; } = WindowBorder.RESIZABLE;
    /// <summary>
    /// Controls the state of the window (normal, minimized, maximized, fullscreen).
    /// </summary>
    /// <remarks>
    /// Note: As of now, this value does not get updated if the user changes the State of the window manually.
    /// </remarks>
    public WindowState WindowState { get; set; } = WindowState.NORMAL; //TODO: update this value if it gets changed

    // implicit convertion to WindowOptions
    public static implicit operator Silk.NET.Windowing.WindowOptions(CodeDrawOptions options) => 
        Silk.NET.Windowing.WindowOptions.Default with
        {
            Title = options.Title,
            Size = options.Size.ToSilkI(),
            Position = options.Position.ToSilkI(),
            TopMost = options.IsAlwaysOnTop,
            WindowBorder = options.WindowBorder switch
            {
                WindowBorder.RESIZABLE => WindowBorderSilk.Resizable,
                WindowBorder.FIXED => WindowBorderSilk.Fixed,
                WindowBorder.HIDDEN => WindowBorderSilk.Hidden,
                _ => WindowBorderSilk.Resizable,
            },
            WindowState = options.WindowState switch
            {
                WindowState.NORMAL => WindowStateSilk.Normal,
                WindowState.MINIMIZED => WindowStateSilk.Minimized,
                WindowState.MAXIMIZED => WindowStateSilk.Maximized,
                WindowState.FULLSCREEN => WindowStateSilk.Fullscreen,
                _ => WindowStateSilk.Normal,
            }
        };
}