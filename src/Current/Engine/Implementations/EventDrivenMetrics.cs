namespace MarcoZechner.CodeDrawDotNet.Engine;

public struct EventDrivenMetrics
{
    public double JobsPerSec;
    public double BusyPercent;
    public double IdleSec;

    public override readonly string ToString() => $"Jobs: {JobsPerSec:0.0}/s, Busy: {BusyPercent:0.0}%, Idle: {IdleSec:0.00}s";
    public readonly string ToShortString()
    {
        if (IdleSec > 0)
            return $"Idel: {IdleSec:0.00}s, Busy: {BusyPercent:0.0}%";
        else
            return $"Jobs: {JobsPerSec:0.0}/s, Busy: {BusyPercent:0.0}%";
    }
}