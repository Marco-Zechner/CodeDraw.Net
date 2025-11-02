using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

public class DrawBench {
    // Imagine this isolates a hot path (no window creation per-iteration!)
    [Benchmark]
    public void ClearAction() {
        // e.g., build a ClearAction and execute against a mock render target
    }
}

public static class Program {
    public static void Main(string[] args) => BenchmarkRunner.Run<DrawBench>();
}
