namespace MarcoZechner.CodeDrawDotNet.Engine.Abstractions;

public interface ILayerMetricsProvider
{
    double JobsPerSec { get; }
    double BusyPercent { get; }
    double IdleSec { get; }
}