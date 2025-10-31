using System.Diagnostics;

namespace MarcoZechner.Diagnostics;
internal sealed class WorkRate
{
    private readonly RateMeter _rate = new(0.25);
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastJobTicks;

    public void OnJob()
    {
        _rate.Tick();
        Volatile.Write(ref _lastJobTicks, _sw.ElapsedTicks);
    }

    public void MaybeSample() => _rate.MaybeSample();

    public double JobsPerSec => _rate.Ewma;

    public double IdleSeconds
    {
        get
        {
            long last = Volatile.Read(ref _lastJobTicks);
            long dt = _sw.ElapsedTicks - last;
            return (double)dt / Stopwatch.Frequency;
        }
    }
}
