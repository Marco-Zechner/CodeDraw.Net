using System.Reflection;
using System.Text;

public interface ITestable
{
    /// <summary>
    /// Execute the test. Throw to indicate an error.
    /// If it returns without throwing, the runner will ask the user
    /// to mark pass/fail unless -s was provided.
    /// </summary>
    void RunTest();
}

namespace MarcoZechner.CodeDrawDotNet.EngineTests
{
    public enum TestOutcome
    {
        Passed,
        Failed,
        Unknown
    }

    public sealed class TestResult
    {
        public required int Index { get; init; }
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

            foreach (var (index, type) in runList)
            {
                var name = PrettyName(type);
                Console.WriteLine($"\n[{index}] {name}");
                Console.WriteLine(new string('-', (int)MathF.Min(80, name.Length + 6)));

                var result = new TestResult
                {
                    Index = index,
                    Name = name,
                    TypeName = type.FullName ?? type.Name,
                    Outcome = TestOutcome.Unknown
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
                        // No user input; mark unknown if no error happened.
                        result.Outcome = TestOutcome.Unknown;
                        result.Note = "Skipped user review (-s).";
                        Console.WriteLine("Result: no exception -> outcome set to 'Unknown' (use -s).");
                    }
                    else
                    {
                        // Ask user to mark pass/fail
                        var passed = PromptYesNo("Mark test as PASSED? [y/n]: ");
                        if (passed)
                        {
                            result.Outcome = TestOutcome.Passed;
                        }
                        else
                        {
                            result.Outcome = TestOutcome.Failed;
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
                    result.Outcome = TestOutcome.Failed;
                    result.Error = FlattenException(ex);
                    Console.WriteLine("Result: FAILED due to exception.");
                }

                results.Add(result);
            }

            PrintSummary(results);
            PrintExitHint();
        }

        // --- Discovery & selection ----------------------------------------------------------

        private static List<(int index, Type type)> DiscoverTests()
        {
            var asm = Assembly.GetExecutingAssembly();

            var testTypes = asm
                .GetTypes()
                .Where(t =>
                    t is { IsAbstract: false, IsInterface: false } &&
                    typeof(ITestable).IsAssignableFrom(t) &&
                    t.GetConstructor(Type.EmptyTypes) is not null)
                .OrderBy(t => ExtractLeadingNumber(t.Name))
                .ThenBy(t => t.Name)
                .ToList();

            var list = new List<(int, Type)>(testTypes.Count);
            for (int i = 0; i < testTypes.Count; i++)
            {
                list.Add((i + 1, testTypes[i]));
            }
            return list;
        }

        private static List<(int index, Type type)> FilterSelection(
            List<(int index, Type type)> all, IReadOnlyList<int> selected)
        {
            if (selected.Count == 0)
                return all;

            var wanted = new HashSet<int>(selected);
            return all.Where(x => wanted.Contains(x.index)).ToList();
        }

        private static int ExtractLeadingNumber(string name)
        {
            int value = 0;
            int i = 0;
            while (i < name.Length && char.IsDigit(name[i]))
            {
                checked { value = value * 10 + (name[i] - '0'); }
                i++;
            }
            return value;
        }

        private static string PrettyName(Type t)
        {
            // Show something like "Test1_OpenWindow" -> "Test1_OpenWindow"
            // (kept simple; you can prettify further if you like)
            return t.Name;
        }

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

                // Unknown arg -> print usage and exit fast
                if (!string.IsNullOrWhiteSpace(a))
                {
                    PrintUsageAndExit($"Unrecognized argument: {a}");
                }
            }

            // normalize/unique
            nums = nums.Distinct().OrderBy(n => n).ToList();
            return new Options(skip, nums);
        }

        private static void PrintUsageAndExit(string? reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Console.WriteLine(reason);

            Console.WriteLine(
                "\nUsage:\n" +
                "  EngineTests [-s] [numbers]\n\n" +
                "Arguments:\n" +
                "  -s          Skip user prompts; mark tests with no exception as 'Unknown'.\n" +
                "  numbers     Space-separated test indices to run (as shown in the menu).\n" +
                "              If omitted, all tests run.\n" +
                "\nExamples:\n" +
                "  EngineTests              # run all tests, ask y/n per test\n" +
                "  EngineTests -s           # run all tests, skip user prompts\n" +
                "  EngineTests 1 3 5        # run tests 1,3,5 and ask y/n per test\n" +
                "  EngineTests -s 2 4       # run tests 2 and 4, skip prompts\n"
            );
            Environment.Exit(1);
        }

        private static bool PromptYesNo(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var line = Console.ReadLine();
                if (line is null) return false; // default to 'no' if piped/no input
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
                Header("Idx", 5) +
                Header("Name", nameWidth + 2) +
                Header("Outcome", outcomeWidth + 2) +
                Header("Duration", 12) +
                "Note/Error"
            );

            Console.WriteLine(new string('-', 5 + nameWidth + 2 + outcomeWidth + 2 + 12 + 10));

            foreach (var r in results.OrderBy(r => r.Index))
            {
                var outcome = r.Outcome.ToString();
                var duration = $"{r.Duration.TotalMilliseconds:F0} ms";
                var note = r.Error ?? r.Note ?? "";

                Console.WriteLine(
                    Header(r.Index.ToString(), 5) +
                    Header(r.Name, nameWidth + 2) +
                    Header(outcome, outcomeWidth + 2) +
                    Header(duration, 12) +
                    note
                );
            }

            var passed = results.Count(r => r.Outcome == TestOutcome.Passed);
            var failed = results.Count(r => r.Outcome == TestOutcome.Failed);
            var unknown = results.Count(r => r.Outcome == TestOutcome.Unknown);

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
}
