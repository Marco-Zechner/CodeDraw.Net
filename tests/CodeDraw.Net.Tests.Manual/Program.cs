using System.Reflection;
using System.Text;
// using MarcoZechner.CodeDrawDotNet.Api;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

public interface ITestable
{
    /// <summary>
    /// Execute the test. Throw to indicate an error.
    /// If it returns without throwing, the runner will ask the user
    /// to mark pass/fail unless -s was provided.
    /// </summary>
    void RunTest();
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OrderAttribute : Attribute
{
    public int Id { get; }
    public OrderAttribute(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Order id must be a positive integer.");
        Id = id;
    }
}

public enum TestOutcome
{
    PASSED,
    FAILED,
    UNKNOWN
}

public sealed class TestResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public TestOutcome Outcome { get; set; }
    public string? Error { get; set; }
    public string? Note { get; set; }
    public TimeSpan Duration { get; set; }
}

public static class Program
{
    private sealed record Options(bool SkipPrompt, IReadOnlyList<int> Selected);

    public static void Main(string[] args)
    {
        Console.Clear();
        var allTests = DiscoverTests();
        if (allTests.Count == 0)
        {
            Console.WriteLine("No tests found. Create classes that implement ITestable with a public parameterless constructor.");
            return;
        }

        var options = ParseArgs(args);
        var runList = FilterSelection(allTests, options.Selected);

        PrintHeader(allTests.Count, runList.Count, options);

        var results = new List<TestResult>(capacity: runList.Count);

        foreach (var (id, type) in runList)
        {
            int consoleTopBefore = Console.CursorTop;

            var name = PrettyName(type);
            Console.WriteLine($"\n[{id}] {name}");
            Console.WriteLine(new string('-', (int)MathF.Min(80, name.Length + 6)));

            var result = new TestResult
            {
                Id = id,
                Name = name,
                TypeName = type.FullName ?? type.Name,
                Outcome = TestOutcome.UNKNOWN
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var instance = (ITestable?)Activator.CreateInstance(type);
                if (instance is null)
                    throw new InvalidOperationException("Failed to construct test (null instance).");

                instance.RunTest();
                // CodeDrawRuntime.CloseAllWindows(); //TODO: fix
                sw.Stop();

                result.Duration = sw.Elapsed;

                if (options.SkipPrompt)
                {
                    result.Outcome = TestOutcome.UNKNOWN;
                    result.Note = "Skipped user review (-s).";
                    Console.WriteLine("Result: no exception -> outcome set to 'Unknown' (due to -s).");
                }
                else
                {
                    var passed = PromptYesNo("Mark test as PASSED? [y/n]: ");
                    if (passed)
                    {
                        result.Outcome = TestOutcome.PASSED;
                    }
                    else
                    {
                        result.Outcome = TestOutcome.FAILED;
                        Console.Write("Reason for failure: ");
                        result.Note = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(result.Note))
                            result.Note = "(no reason provided)";
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Duration = sw.Elapsed;
                result.Outcome = TestOutcome.FAILED;
                result.Error = FlattenException(ex);
                Console.WriteLine("Result: FAILED due to exception.");
            }

            int consoleTopAfter = Console.CursorTop;
            Console.SetCursorPosition(0, consoleTopBefore);
            for (int i = 0; i < consoleTopAfter - consoleTopBefore; i++)
            {
                Console.WriteLine(new string(' ', Console.WindowWidth-1));
            }
            Console.SetCursorPosition(0, consoleTopBefore);

            results.Add(result);
        }

        PrintSummary(results);
        PrintExitHint();
    }

    // --- Discovery & selection ----------------------------------------------------------

    private static List<(int id, Type type)> DiscoverTests()
    {
        var asm = Assembly.GetExecutingAssembly();

        var candidates = asm
            .GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsInterface: false } &&
                typeof(ITestable).IsAssignableFrom(t) &&
                t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();

        // Partition: with [Order] and without
        var withAttr = new List<(int id, Type type)>();
        var withoutAttr = new List<Type>();

        foreach (var t in candidates)
        {
            var attr = t.GetCustomAttribute<OrderAttribute>();
            if (attr is not null)
            {
                withAttr.Add((attr.Id, t));
            }
            else
            {
                withoutAttr.Add(t);
            }
        }

        // Resolve ID collisions for attributed tests: bump to next free id
        var used = new HashSet<int>();
        var fixedWithAttr = new List<(int id, Type type)>(withAttr.Count);

        foreach (var (id, type) in withAttr.OrderBy(x => x.id).ThenBy(x => x.type.Name))
        {
            int assigned = id;
            if (assigned <= 0) assigned = 1;

            while (!used.Add(assigned))
            {
                assigned++;
            }

            if (assigned != id)
            {
                Console.WriteLine($"[warning] Duplicate [Order({id})] detected. '{type.Name}' reassigned to id {assigned}.");
            }

            fixedWithAttr.Add((assigned, type));
        }

        // Assign IDs to tests without attribute, after the highest used id
        int next = used.Count == 0 ? 1 : (used.Max() + 1);
        foreach (var t in withoutAttr.OrderBy(t => t.Name))
        {
            while (!used.Add(next)) next++;
            fixedWithAttr.Add((next, t));
            next++;
        }

        // Return sorted by id
        return fixedWithAttr.OrderBy(x => x.id).ToList();
    }

    private static List<(int id, Type type)> FilterSelection(
        List<(int id, Type type)> all, IReadOnlyList<int> selected)
    {
        if (selected.Count == 0)
            return all;

        var wanted = new HashSet<int>(selected);
        return all.Where(x => wanted.Contains(x.id)).ToList();
    }

    private static string PrettyName(Type t) => t.Name;

    // --- Args & prompts ----------------------------------------------------------------

    private static Options ParseArgs(string[] args)
    {
        var skip = false;
        var nums = new List<int>();

        foreach (var a in args)
        {
            if (a.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                skip = true;
                continue;
            }

            if (int.TryParse(a, out var n) && n > 0)
            {
                nums.Add(n);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(a))
            {
                PrintUsageAndExit($"Unrecognized argument: {a}");
            }
        }

        nums = nums.Distinct().OrderBy(n => n).ToList();
        return new Options(skip, nums);
    }

    private static void PrintUsageAndExit(string? reason = null)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            Console.WriteLine(reason);

        Console.WriteLine(
            "\nUsage:\n" +
            "  EngineTests [-s] [ids]\n\n" +
            "Arguments:\n" +
            "  -s          Skip user prompts; mark tests with no exception as 'Unknown'.\n" +
            "  ids         Space-separated test IDs to run (from [Order(id)] or auto-assigned).\n" +
            "              If omitted, all tests run.\n" +
            "\nExamples:\n" +
            "  EngineTests              # run all tests, ask y/n per test\n" +
            "  EngineTests -s           # run all tests, skip user prompts\n" +
            "  EngineTests 1 3 5        # run test IDs 1,3,5 and ask y/n per test\n" +
            "  EngineTests -s 2 4       # run IDs 2 and 4, skip prompts\n"
        );
        Environment.Exit(1);
    }

    private static bool PromptYesNo(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var line = Console.ReadLine();
            if (line is null) return false; // piped/no input -> default 'no'
            line = line.Trim().ToLowerInvariant();
            if (line is "y" or "yes") return true;
            if (line is "n" or "no") return false;
            Console.WriteLine("Please type 'y' or 'n'.");
        }
    }

    // --- Output formatting --------------------------------------------------------------

    private static void PrintHeader(int totalCount, int runCount, Options opt)
    {
        Console.WriteLine($"Discovered {totalCount} test(s). Will run: {runCount}.");
        Console.WriteLine($"User prompts: {(opt.SkipPrompt ? "skipped (-s)" : "enabled")}");
    }

    private static void PrintSummary(List<TestResult> results)
    {
        Console.WriteLine("\n======================== SUMMARY ========================");
        int nameWidth = (int)MathF.Max(10, results.Max(r => r.Name.Length));
        int outcomeWidth = 7;

        string Header(string title, int width) =>
            title + new string(' ', (int)MathF.Max(1, width - title.Length));

        Console.WriteLine(
            Header("ID", 5) +
            Header("Name", nameWidth + 2) +
            Header("Outcome", outcomeWidth + 2) +
            Header("Duration", 12) +
            "Note/Error"
        );

        Console.WriteLine(new string('-', 5 + nameWidth + 2 + outcomeWidth + 2 + 12 + 10));

        foreach (var r in results.OrderBy(r => r.Id))
        {
            var outcome = r.Outcome.ToString();
            var duration = $"{r.Duration.TotalMilliseconds:F0} ms";
            var note = r.Error ?? r.Note ?? "";

            Console.WriteLine(
                Header(r.Id.ToString(), 5) +
                Header(r.Name, nameWidth + 2) +
                Header(outcome, outcomeWidth + 2) +
                Header(duration, 12) +
                note
            );
        }

        var passed = results.Count(r => r.Outcome == TestOutcome.PASSED);
        var failed = results.Count(r => r.Outcome == TestOutcome.FAILED);
        var unknown = results.Count(r => r.Outcome == TestOutcome.UNKNOWN);

        Console.WriteLine("\nTotals:");
        Console.WriteLine($"  Passed : {passed}");
        Console.WriteLine($"  Failed : {failed}");
        Console.WriteLine($"  Unknown: {unknown}");
        Console.WriteLine("=========================================================");
    }

    private static void PrintExitHint()
    {
        Console.WriteLine("\nDone.");
    }

    // --- Helpers -----------------------------------------------------------------------

    private static string FlattenException(Exception ex)
    {
        var sb = new StringBuilder();
        int depth = 0;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (depth++ > 0) sb.AppendLine("----- Inner Exception -----");
            sb.AppendLine(e.GetType().FullName);
            sb.AppendLine(e.Message);
            sb.AppendLine(e.StackTrace);
        }
        return sb.ToString();
    }
}
