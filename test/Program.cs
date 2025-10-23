using MarcoZechner.CodeDrawDotNet.Test1;
using MarcoZechner.CodeDrawDotNet.Test2;
using MarcoZechner.Math;

namespace MarcoZechner.CodeDrawDotNet.Tests;

public class Program
{
    private static readonly (string key, string name, Action action)[] _tests =
    [
        ("1", "Simple CodeDraw", Test1_CodeDraw.Run),
        ("2", "Internal Loop", Test2_CodeDraw.Run),
    ];


    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            foreach (var arg in args)
            {
                var (key, name, action) = _tests.FirstOrDefault(t => t.key == arg);
                if (action != null)
                {
                    Console.WriteLine($"Running Test {key}: {name}");
                    Console.WriteLine(new string('-', Console.WindowWidth - 1));
                    action();
                    Console.WriteLine(new string('-', Console.WindowWidth - 1));
                }
            }

            Test1_CodeDraw.OffsetNow();
            Test2_CodeDraw.OffsetNow();

            int maxDiff = 200;
            int barLength = (Console.WindowWidth - 5) / 2;
            while (true)
            {
                long fc1 = Test1_CodeDraw.FrameCount;
                long fc2 = Test2_CodeDraw.FrameCount;
                long toLeftSide = fc1 - fc2;
                long toRightSide = fc2 - fc1;
                long leftEmpty = MathG.Min(barLength - toLeftSide, barLength);
                long rightEmpty = MathG.Min(barLength - toRightSide, barLength);
                Console.Write($"\n1:{new string(' ', (int)leftEmpty)}{new string('=', (int)(barLength - leftEmpty))}|" +
                              $"{new string('=', (int)(barLength - rightEmpty))}{new string(' ', (int)rightEmpty)}:2");
                Thread.Sleep(20);
            }

            Console.WriteLine("Waiting for open windows to close...");
            CodeDraw.WaitForOpenWindows();
            return;
        }

        foreach (var (key, name, _) in _tests)
            Console.WriteLine($"{key}:\t{name}");
        Console.WriteLine();
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            var (key, name, action) = _tests.FirstOrDefault(t => t.key == input);
            if (action == null)
            {
                Console.WriteLine($"Invalid Input: '{input}'");
                continue;
            }

            Console.WriteLine($"Running Test {key}: {name}");
            Console.WriteLine(new string('-', Console.WindowWidth - 1));
            action();
            Console.WriteLine(new string('-', Console.WindowWidth - 1));
            continue;
        }
    }
}