using System.Diagnostics;
using System.Reflection;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual;

// -------------------------------------------------------------------------------------------------
// Prototype Runner
// - Put [Prototype("uniqueName")] on ANY *instance* method (0 params).
// - Put [StaticPrototype("uniqueName")] on ANY *static* method (0 params).
// - Put [ConstructorPrototype("uniqueName")] directly on a *constructor* (public, instance, 0 params).
// - Names must be unique across ALL three attributes. Duplicates => throw at discovery.
// - CLI: exactly one argument: <uniqueName>
// - If no args: prints available names.
// - Result: exception => FAIL (exit code 1), no exception => PASS (exit code 0).
// -------------------------------------------------------------------------------------------------

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PrototypeAttribute : Attribute
{
    public string UniqueName { get; }

    public PrototypeAttribute(string uniqueName)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
            throw new ArgumentException("Prototype uniqueName must be non-empty.", nameof(uniqueName));
        UniqueName = uniqueName.Trim();
    }

    public PrototypeAttribute(int uniqueName) : this(uniqueName.ToString()) { }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class StaticPrototypeAttribute : Attribute
{
    public string UniqueName { get; }

    public StaticPrototypeAttribute(string uniqueName)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
            throw new ArgumentException("StaticPrototype uniqueName must be non-empty.", nameof(uniqueName));
        UniqueName = uniqueName.Trim();
    }

    public StaticPrototypeAttribute(int uniqueName) : this(uniqueName.ToString()) { }
}

[AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
public sealed class ConstructorPrototypeAttribute : Attribute
{
    public string UniqueName { get; }

    public ConstructorPrototypeAttribute(string uniqueName)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
            throw new ArgumentException("ConstructorPrototype uniqueName must be non-empty.", nameof(uniqueName));
        UniqueName = uniqueName.Trim();
    }

    public ConstructorPrototypeAttribute(int uniqueName) : this(uniqueName.ToString()) { }
}

public static class Program
{
    private enum EntryKind
    {
        StaticMethod,
        InstanceMethod,
        Constructor
    }

    private sealed record Entry(
        string Name,
        EntryKind Kind,
        Type DeclaringType,
        MethodInfo? Method,
        ConstructorInfo? Ctor
    );

    public static int Main(string[] args)
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

        var entries = DiscoverOrThrow(Assembly.GetExecutingAssembly());

        if (args.Length == 0)
        {
            PrintUsage(entries);
            return 1;
        }

        if (args.Length != 1)
        {
            Console.WriteLine("Error: expected exactly 1 argument: <uniqueName>.");
            PrintUsage(entries);
            return 1;
        }

        var name = args[0].Trim();
        if (!entries.TryGetValue(name, out var entry))
        {
            Console.WriteLine($"Error: no prototype found with name '{name}'.");
            PrintUsage(entries);
            return 1;
        }

        Console.WriteLine($"Running: {entry.Name}");
        Console.WriteLine($"  {FormatSignature(entry)}");

        var sw = Stopwatch.StartNew();
        try
        {
            Invoke(entry);
            sw.Stop();
            Console.WriteLine($"PASS ({sw.Elapsed.TotalMilliseconds:F0} ms)");
            return 0;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"FAIL ({sw.Elapsed.TotalMilliseconds:F0} ms)");
            Console.WriteLine(FlattenException(UnwrapInvoke(ex)));
            return 1;
        }
    }

    private static Dictionary<string, Entry> DiscoverOrThrow(Assembly asm)
    {
        var dict = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var t in asm.GetTypes().Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            const BindingFlags METHOD_FLAGS =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            // -------- methods (static + instance) --------
            foreach (var m in t.GetMethods(METHOD_FLAGS))
            {
                if (m.IsSpecialName) continue; // property/event operators etc.

                var p = m.GetCustomAttribute<PrototypeAttribute>(inherit: false);
                var s = m.GetCustomAttribute<StaticPrototypeAttribute>(inherit: false);

                if (p is null && s is null) continue;
                if (p is not null && s is not null)
                    throw new InvalidOperationException(
                        $"Method '{t.FullName}.{m.Name}' cannot have BOTH [Prototype] and [StaticPrototype]. Pick one.");

                var isStatic = s is not null;
                var name = (p?.UniqueName ?? s!.UniqueName);

                ValidateMethodSignatureOrThrow(t, m, isStatic, name);

                var entry = new Entry(
                    Name: name,
                    Kind: isStatic ? EntryKind.StaticMethod : EntryKind.InstanceMethod,
                    DeclaringType: t,
                    Method: m,
                    Ctor: null
                );

                AddOrThrowCollision(dict, entry);
            }

            // -------- constructors --------
            // You want the attribute above the ctor, so we scan ctors.
            const BindingFlags CTOR_FLAGS =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (var c in t.GetConstructors(CTOR_FLAGS))
            {
                var cp = c.GetCustomAttribute<ConstructorPrototypeAttribute>(inherit: false);
                if (cp is null) continue;

                var name = cp.UniqueName;
                ValidateConstructorSignatureOrThrow(t, c, name);

                var entry = new Entry(
                    Name: name,
                    Kind: EntryKind.Constructor,
                    DeclaringType: t,
                    Method: null,
                    Ctor: c
                );

                AddOrThrowCollision(dict, entry);
            }
        }

        return dict;
    }

    private static void AddOrThrowCollision(Dictionary<string, Entry> dict, Entry entry)
    {
        if (dict.TryGetValue(entry.Name, out var existing))
        {
            throw new InvalidOperationException(
                "Duplicate prototype name detected: '" + entry.Name + "'.\n" +
                "  Existing: " + FormatSignature(existing) + "\n" +
                "  New     : " + FormatSignature(entry)
            );
        }

        dict.Add(entry.Name, entry);
    }

    private static void ValidateMethodSignatureOrThrow(Type declaringType, MethodInfo m, bool shouldBeStatic, string uniqueName)
    {
        if (m.GetParameters().Length != 0)
            throw new InvalidOperationException(
                $"Prototype '{uniqueName}' invalid: '{declaringType.FullName}.{m.Name}' must have 0 parameters.");

        if (m.IsStatic != shouldBeStatic)
        {
            var expected = shouldBeStatic ? "static" : "instance";
            throw new InvalidOperationException(
                $"Prototype '{uniqueName}' invalid: '{declaringType.FullName}.{m.Name}' is not {expected}, but attribute requires it.");
        }

        // Allow void, Task, ValueTask
        var rt = m.ReturnType;
        var ok = rt == typeof(void) || rt == typeof(Task) || rt == typeof(ValueTask);
        if (!ok)
            throw new InvalidOperationException(
                $"Prototype '{uniqueName}' invalid: '{declaringType.FullName}.{m.Name}' must return void, Task, or ValueTask (got '{rt.FullName}').");

        // For instance methods we will construct the type => needs public parameterless ctor.
        if (!shouldBeStatic)
        {
            if (declaringType.GetConstructor(Type.EmptyTypes) is null)
                throw new InvalidOperationException(
                    $"Prototype '{uniqueName}' invalid: '{declaringType.FullName}' must have a public parameterless constructor for instance prototypes.");
        }
    }

    private static void ValidateConstructorSignatureOrThrow(Type declaringType, ConstructorInfo c, string uniqueName)
    {
        if (c.IsStatic)
            throw new InvalidOperationException(
                $"ConstructorPrototype '{uniqueName}' invalid: '{declaringType.FullName}..cctor' is static. Use an instance ctor.");

        if (!c.IsPublic)
            throw new InvalidOperationException(
                $"ConstructorPrototype '{uniqueName}' invalid: '{declaringType.FullName}..ctor' must be public.");

        if (c.GetParameters().Length != 0)
            throw new InvalidOperationException(
                $"ConstructorPrototype '{uniqueName}' invalid: '{declaringType.FullName}..ctor' must have 0 parameters.");
    }

    private static void Invoke(Entry e)
    {
        switch (e.Kind)
        {
            case EntryKind.StaticMethod:
                InvokeMethod(method: e.Method!, instance: null);
                return;

            case EntryKind.InstanceMethod:
            {
                object? instance = Activator.CreateInstance(e.DeclaringType);
                if (instance is null)
                    throw new InvalidOperationException($"Failed to create instance of '{e.DeclaringType.FullName}'.");
                InvokeMethod(method: e.Method!, instance: instance);
                return;
            }

            case EntryKind.Constructor:
            {
                // Invoke ctor, then immediately dispose if possible.
                // If your ctor opens windows and blocks until closed, this is perfect.
                // If not, it's still fine: the ctor is the "entry point" you asked for.
                object? instance = null;
                try
                {
                    instance = e.Ctor!.Invoke(parameters: null);
                    if (instance is null)
                        throw new InvalidOperationException($"Constructor returned null for '{e.DeclaringType.FullName}'. (This should never happen.)");
                }
                finally
                {
                    if (instance is IAsyncDisposable ad)
                    {
                        // Keep it simple: sync wait.
                        ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    else if (instance is IDisposable d)
                    {
                        d.Dispose();
                    }
                }

                return;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(e.Kind), e.Kind, "Unknown entry kind.");
        }
    }

    private static void InvokeMethod(MethodInfo method, object? instance)
    {
        var result = method.Invoke(instance, parameters: null);

        // Support async-ish prototypes without adding a new runner mode.
        if (result is Task task)
            task.GetAwaiter().GetResult();
        else if (result is ValueTask vt)
            vt.GetAwaiter().GetResult();
    }

    private static void PrintUsage(Dictionary<string, Entry> entries)
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  EngineTests <uniqueName>");
        Console.WriteLine();
        Console.WriteLine("Available prototypes:");

        if (entries.Count == 0)
        {
            Console.WriteLine("  (none found)");
            Console.WriteLine();
            Console.WriteLine("Add [Prototype(\"name\")] to an instance method, [StaticPrototype(\"name\")] to a static method,");
            Console.WriteLine("or [ConstructorPrototype(\"name\")] to a public parameterless constructor.");
            return;
        }

        foreach (var e in entries.Values.OrderBy(e => e.Name, StringComparer.Ordinal))
            Console.WriteLine("  " + e.Name + "   ->   " + FormatSignature(e));

        Console.WriteLine();
    }

    private static string FormatSignature(Entry e)
    {
        return e.Kind switch
        {
            EntryKind.StaticMethod =>
                $"{Access(e.Method!)} static {Ret(e.Method!)} {e.DeclaringType.FullName}.{e.Method!.Name}()",

            EntryKind.InstanceMethod =>
                $"{Access(e.Method!)} instance {Ret(e.Method!)} {e.DeclaringType.FullName}.{e.Method!.Name}()",

            EntryKind.Constructor =>
                $"public ctor {e.DeclaringType.FullName}..ctor()",

            _ => $"{e.DeclaringType.FullName} (unknown)"
        };

        static string Access(MethodInfo m) => m.IsPublic ? "public" : "non-public";
        static string Ret(MethodInfo m) => m.ReturnType == typeof(void) ? "void" : m.ReturnType.Name;
    }

    private static Exception UnwrapInvoke(Exception ex)
    {
        // MethodInfo.Invoke wraps thrown exceptions in TargetInvocationException.
        if (ex is TargetInvocationException tie && tie.InnerException is not null)
            return tie.InnerException;
        return ex;
    }

    private static string FlattenException(Exception ex)
    {
        var lines = new List<string>();
        var depth = 0;

        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (depth++ > 0) lines.Add("----- Inner Exception -----");
            lines.Add(e.GetType().FullName ?? e.GetType().Name);
            lines.Add(e.Message);
            if (!string.IsNullOrWhiteSpace(e.StackTrace))
                lines.Add(e.StackTrace);
        }

        return string.Join(Environment.NewLine, lines);
    }
}