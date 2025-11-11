namespace MarcoZechner.CodeDrawDotNet.Interfaces;

public interface ILayerMetricsProvider
{
    double JobsPerSec { get; }
    double BusyPercent { get; }
    double IdleSec { get; }
}