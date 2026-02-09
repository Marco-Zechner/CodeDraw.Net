using System.Reflection;
using System.Text;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

// ---------------------------------------------------------
// Common contracts
// ---------------------------------------------------------

public interface ITestable
{
    /// <summary>
    /// Execute the test. Throw to indicate an error.
    /// If it returns without throwing, the runner will ask the user
    /// to mark pass/fail unless -s was provided.
    /// </summary>
    void RunTest();
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
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

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PrototypeAttribute : Attribute
{
    public int Id { get; }
    public PrototypeAttribute(int id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Prototype id must be a positive integer.");
        Id = id;
    }
}

/// <summary>
/// Marks a public static prototype runner method.
/// Expected signature: public static void RunTest()
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class StaticPrototypeAttribute : Attribute { }

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
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.WriteLine("=== UNHANDLED EXCEPTION ===");
            Console.WriteLine(e.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.WriteLine("=== UNOBSERVED TASK EXCEPTION ===");
            Console.WriteLine(e.Exception);
            e.SetObserved();
        };

        // ---------------------------------------------
        // Prototype mode (-p): run a single prototype
        // Now supports:
        //  1) ITestable instance with RunTest()
        //  2) [StaticPrototype] public static void RunTest()
        // ---------------------------------------------
        if (args.Any(s => s.Equals("-p", StringComparison.OrdinalIgnoreCase)))
        {
            var allPrototypes = DiscoverPrototypes();
            if (allPrototypes.Count == 0)
            {
                Console.WriteLine("No prototypes found. Add [Prototype(id)] to a class that either implements ITestable, or has a [StaticPrototype] public static void RunTest().");
                return;
            }

            var selectedId = args.Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .FirstOrDefault();

            var target = selectedId != 0
                ? allPrototypes.FirstOrDefault(t => t.Id == selectedId)
                : allPrototypes.First();

            if (target.Type is null)
            {
                Console.WriteLine("No prototype found with id " + selectedId);
                return;
            }

            try
            {
                RunPrototype(target);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Prototype execution FAILED due to exception:");
                Console.WriteLine(FlattenException(ex));
            }
            return;
        }

        // ---------------------------------------------
        // Test runner mode: unchanged (ITestable only)
        // ---------------------------------------------
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
            var consoleTopBefore = Console.CursorTop;

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

            var consoleTopAfter = Console.CursorTop;
            Console.SetCursorPosition(0, consoleTopBefore);
            for (var i = 0; i < consoleTopAfter - consoleTopBefore; i++)
            {
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            }
            Console.SetCursorPosition(0, consoleTopBefore);

            results.Add(result);
        }

        PrintSummary(results);
        PrintExitHint();
    }

    // ---------------------------------------------------------
    // Prototype execution
    // ---------------------------------------------------------

    private readonly struct PrototypeInfo(int id, Type type, MethodInfo? staticRun)
    {
        public readonly int Id = id;
        public readonly Type Type = type;
        public readonly MethodInfo? StaticRun = staticRun;
    }

    private static void RunPrototype(PrototypeInfo p)
    {
        // Prefer static runner if available
        if (p.StaticRun is not null)
        {
            p.StaticRun.Invoke(null, null);
            return;
        }

        // Fallback: ITestable instance runner
        var instance = (ITestable?)Activator.CreateInstance(p.Type);
        if (instance is null)
            throw new InvalidOperationException("Failed to construct prototype (null instance).");

        instance.RunTest();
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

        var withAttr = new List<(int id, Type type)>();
        var withoutAttr = new List<Type>();

        foreach (var t in candidates)
        {
            var attr = t.GetCustomAttribute<OrderAttribute>();
            if (attr is not null) withAttr.Add((attr.Id, t));
            else withoutAttr.Add(t);
        }

        var used = new HashSet<int>();
        var fixedWithAttr = new List<(int id, Type type)>(withAttr.Count);

        foreach (var (id, type) in withAttr.OrderBy(x => x.id).ThenBy(x => x.type.Name))
        {
            var assigned = id <= 0 ? 1 : id;
            while (!used.Add(assigned)) assigned++;

            if (assigned != id)
                Console.WriteLine($"[warning] Duplicate [Order({id})] detected. '{type.Name}' reassigned to id {assigned}.");

            fixedWithAttr.Add((assigned, type));
        }

        var next = used.Count == 0 ? 1 : (used.Max() + 1);
        foreach (var t in withoutAttr.OrderBy(t => t.Name))
        {
            while (!used.Add(next)) next++;
            fixedWithAttr.Add((next, t));
            next++;
        }

        return fixedWithAttr.OrderBy(x => x.id).ToList();
    }

    private static List<PrototypeInfo> DiscoverPrototypes()
    {
        var asm = Assembly.GetExecutingAssembly();

        // Only classes explicitly marked as prototypes
        var protoTypes = asm
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } &&
                        t.GetCustomAttribute<PrototypeAttribute>() is not null)
            .ToList();

        var withAttr = new List<(int id, PrototypeInfo info)>();

        foreach (var t in protoTypes)
        {
            var attr = t.GetCustomAttribute<PrototypeAttribute>()!;
            var staticRun = FindStaticPrototypeRunner(t);

            // Validation: must have either static runner OR ITestable+ctor
            var hasInstanceRunner = typeof(ITestable).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null;
            if (staticRun is null && !hasInstanceRunner)
            {
                Console.WriteLine($"[warning] [Prototype({attr.Id})] on '{t.Name}' ignored: needs either [StaticPrototype] public static void RunTest(), or implement ITestable with public parameterless ctor.");
                continue;
            }

            withAttr.Add((attr.Id, new PrototypeInfo(attr.Id, t, staticRun)));
        }

        // Resolve ID collisions: bump to next free id
        var used = new HashSet<int>();
        var fixedList = new List<PrototypeInfo>(withAttr.Count);

        foreach (var (id, info) in withAttr.OrderBy(x => x.id).ThenBy(x => x.info.Type.Name))
        {
            var assigned = id <= 0 ? 1 : id;
            while (!used.Add(assigned)) assigned++;

            if (assigned != id)
                Console.WriteLine($"[warning] Duplicate [Prototype({id})] detected. '{info.Type.Name}' reassigned to id {assigned}.");

            fixedList.Add(new PrototypeInfo(assigned, info.Type, info.StaticRun));
        }

        return fixedList.OrderBy(x => x.Id).ToList();
    }

    private static MethodInfo? FindStaticPrototypeRunner(Type t)
    {
        // Find exactly one: public static void RunTest() with [StaticPrototype]
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);

        var candidates = methods
            .Where(m =>
                m.GetCustomAttribute<StaticPrototypeAttribute>() is not null &&
                m.Name == "RunTest" &&
                m.ReturnType == typeof(void) &&
                m.GetParameters().Length == 0)
            .ToList();

        if (candidates.Count == 0) return null;

        if (candidates.Count > 1)
        {
            Console.WriteLine($"[warning] '{t.Name}' has multiple [StaticPrototype] RunTest methods. Using the first one.");
        }

        return candidates[0];
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
            "  EngineTests [-s] [ids]\n" +
            "  EngineTests -p [id]\n\n" +
            "Arguments:\n" +
            "  -p          Prototype mode: run one [Prototype(id)] (supports static or instance runner).\n" +
            "  -s          Skip user prompts; mark tests with no exception as 'Unknown'.\n" +
            "  ids         Space-separated test IDs to run (from [Order(id)] or auto-assigned).\n" +
            "              If omitted, all tests run.\n" +
            "\nExamples:\n" +
            "  EngineTests              # run all tests, ask y/n per test\n" +
            "  EngineTests -s           # run all tests, skip user prompts\n" +
            "  EngineTests 1 3 5        # run test IDs 1,3,5 and ask y/n per test\n" +
            "  EngineTests -s 2 4       # run IDs 2 and 4, skip prompts\n" +
            "  EngineTests -p           # run first prototype\n" +
            "  EngineTests -p 1         # run prototype id 1\n"
        );
        Environment.Exit(1);
    }

    private static bool PromptYesNo(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var line = Console.ReadLine();
            if (line is null) return false;
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
        var nameWidth = (int)MathF.Max(10, results.Max(r => r.Name.Length));
        var outcomeWidth = 7;

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
        var depth = 0;
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (depth++ > 0) sb.AppendLine("\n----- Inner Exception -----");
            sb.AppendLine(e.GetType().FullName);
            sb.AppendLine(e.Message);
            sb.AppendLine(e.StackTrace);
        }
        return sb.ToString();
    }
}
