using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes;

public interface IGlExecutor
{
    void Run(Action<GL> action);
    T Run<T>(Func<GL, T> func);
}