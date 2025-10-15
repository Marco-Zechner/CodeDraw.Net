using MarcoZechner.CodeDrawDotNet.Test1;
using MarcoZechner.CodeDrawDotNet.Test2;

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
            var (key, name, action) = _tests.FirstOrDefault(t => t.key == args[0]);
            if (action != null)
            {
                Console.WriteLine($"Running Test {key}: {name}");
                Console.WriteLine(new string('-', Console.WindowWidth - 1));
                action();
                Console.WriteLine(new string('-', Console.WindowWidth - 1));
                Console.WriteLine("Waiting for open windows to close...");
                CodeDraw.WaitForOpenWindows();
                return;
            }
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