using MarcoZechner.CodeDrawDotNet;
using MarcoZechner.CodeDrawDotNet.Api.Events;
using MarcoZechner.ColorLib;

namespace MarcoZechner.Tests;

[Order(1)]
class Test1_OpenWindow : ITestable
{
    public void RunTest()
    {
        var win = new CodeDrawWindow("Test1_OpenWindow")
        {
            Size = new(640, 360),
            Resizable = true,
            TargetFPS = 60
        };
        Console.WriteLine("1      Created CodeDrawWindow");

        win.Loaded += (w, gl, glfw, window) =>
        {
            Console.WriteLine($"2      Loaded event fired");
        };

        CodeDrawEvents.Loaded += (w, gl, glfw, window) =>
        {
            Console.WriteLine($"3      Loaded event fired globally");
        };

        win.CloseRequested += (w, args, reason) =>
        {
            if (reason == CloseReason.UserClosedWindow) Console.WriteLine("10.3   CloseRequested: User initiated close (X/Alt+F4)");
            Console.WriteLine($"11     CloseRequested event fired, reason: {reason}");
        };

        CodeDrawEvents.CloseRequested += (w, args, reason) =>
        {
            Console.WriteLine($"12     CloseRequested event fired globally, reason: {reason}");
        };

        win.Closed += () =>
        {
            Console.WriteLine($"13     Closed event fired");
        };

        CodeDrawEvents.Closed += (w) =>
        {
            Console.WriteLine($"14     Closed event fired globally");
        };

        win.Key += (k, sc, a, m) =>
        {
            if (k == Silk.NET.GLFW.Keys.Escape && a == Silk.NET.GLFW.InputAction.Press)
            {
                Console.WriteLine("10.1   Escape pressed, requesting close…");
                win.Close();
            }
        };

        win.Open(); // starts render thread
        Console.WriteLine("4      Opened CodeDrawWindow");
        win.EnqueueGL(gl =>
        {
            Console.WriteLine("6/7    EnqueueGL: setting clear color to white");
        });
        win.Clear(new Color(1f, 1f, 1f, 1f)); // clear to white
        Console.WriteLine("5      Enqueued Drawing Calls");
        win.Show();
        Console.WriteLine("6/7    Showed Window (aka pushed drawing calls to renderloop)");
        win.WaitForRender();
        Console.WriteLine("8      First frame rendered");

        Console.WriteLine("9      Expected: window opens, clears to white background, events work (onloadcalled) (move/resize), Close (ENTER/X) ");
        switch (win.WaitForClose((k) =>
        {
            Console.WriteLine($"10.2   Key pressed: {k.Key}");
            return k.Key == ConsoleKey.Enter;
        }))
        {
            case CloseReason.Unknown:
                Console.WriteLine("?      CloseReason.Unknown");
                break;
            case CloseReason.RequestedByUser:
                Console.WriteLine("15.1   CloseReason.RequestedByUser");
                break;
            case CloseReason.UserClosedWindow:
                Console.WriteLine("15.3   CloseReason.UserClosedWindow");
                break;
            case CloseReason.WaitForCloseEvent:
                Console.WriteLine("15.2   CloseReason.WaitForCloseEvent");
                break;
            case CloseReason.AlreadyClosed:
                Console.WriteLine("17     CloseReason.AlreadyClosed");
                break;
        }
        Console.WriteLine("16     Window Closed. Press ENTER to exit…");

        switch (win.WaitForClose((k) =>
        {
            Console.WriteLine($"10.2   Key pressed: {k.Key}");
            return k.Key == ConsoleKey.Enter;
        }))
        {
            case CloseReason.Unknown:
                Console.WriteLine("?      CloseReason.Unknown");
                break;
            case CloseReason.RequestedByUser:
                Console.WriteLine("15.1   CloseReason.RequestedByUser");
                break;
            case CloseReason.UserClosedWindow:
                Console.WriteLine("15.3   CloseReason.UserClosedWindow");
                break;
            case CloseReason.WaitForCloseEvent:
                Console.WriteLine("15.2   CloseReason.WaitForCloseEvent");
                break;
            case CloseReason.AlreadyClosed:
                Console.WriteLine("17     CloseReason.AlreadyClosed");
                break;
        }

        Console.ReadLine();
    }

}