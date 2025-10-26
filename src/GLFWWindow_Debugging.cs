using System.Diagnostics;

namespace MarcoZechner.CodeDrawDotNet;

public unsafe partial class GLFWWindow
{
    public bool MonitorRendering = false;
    
    private double _dtInternalRender = 0;
    private double _dtLoop = 0;
    private double _dtWait = 0;
    private double _dt;
    private readonly Stopwatch _stopwatch = new();


    #region Debugging
    private readonly List<double> _fpsTimes = [];
    private readonly Stopwatch _consolePrintStopwatch = Stopwatch.StartNew();
    private void RenderMonitor()
    {
        // Debug rendering time
        int max = 150;
        int fps = (int)(1000.0 / _dt);
        _fpsTimes.Add(_dt);
        if (_fpsTimes.Count > 100)
            _fpsTimes.RemoveAt(0);
        int fpsAvg = (int)(1000.0 / _fpsTimes.Average());

        if (_consolePrintStopwatch.ElapsedMilliseconds < 1000 / 10) // 10 times per second
            return;

        Console.Write('[');
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(new string('#', (int)_dtInternalRender));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(new string('#', (int)_dtLoop));
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write(new string('#', (int)_dtWait));
        Console.ResetColor();
        Console.Write(new string(' ', (int)MathF.Max(0, max - (int)_dtInternalRender - (int)_dtLoop - (int)_dtWait)));
        Console.ResetColor();
        Console.Write($"] {_dt:00.00}ms (int: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{_dtInternalRender:00.00}");
        Console.ResetColor();
        Console.Write("ms, loop: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{_dtLoop:00.00}");
        Console.ResetColor();
        Console.Write("ms, wait: ");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{_dtWait:00.00}");
        Console.ResetColor();
        Console.Write("ms) - ");
        Console.ForegroundColor = fps switch
        {
            >= 60 => ConsoleColor.Green,
            >= 30 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };
        Console.Write($"{fps}");
        Console.ResetColor();
        Console.Write(" FPS - Avg: ");
        Console.ForegroundColor = fpsAvg switch
        {
            >= 60 => ConsoleColor.Green,
            >= 30 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red,
        };
        Console.Write($"{fpsAvg}");
        Console.ResetColor();
        Console.WriteLine(" FPS");

        _consolePrintStopwatch.Restart();
    }
    #endregion
}