namespace MarcoZechner.CodeDrawDotNet.Old1;

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

    private static readonly Dictionary<int, (string old, string now)> _logs = [];

    public static void LogChange(string message, int lineTop)
    {
        if (!_logs.ContainsKey(lineTop))
        {
            _logs[lineTop] = (message, message);
        }
        else
        {
            _logs[lineTop] = (_logs[lineTop].now, message);
        }

        var current = Console.GetCursorPosition();
        Console.SetCursorPosition(0, lineTop);
        LogLine("Old: " + _logs[lineTop].old);
        LogLine("Now: " + _logs[lineTop].now);
        Console.SetCursorPosition(current.Left, current.Top);
    }
}