namespace MarcoZechner.CodeDrawDotNet;

public static class Logger
{
    public static void LogLine(string message)
    {
        var now = DateTime.Now;
        Console.WriteLine($"[{now:HH:mm:ss:fff}] " + message);
    }
    public static void Log(string message)
    {
        var now = DateTime.Now;
        Console.Write($"[{now:HH:mm:ss:fff}] " + message);
    }
}