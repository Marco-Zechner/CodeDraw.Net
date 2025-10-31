using System.Diagnostics;

namespace MarcoZechner.Diagnostics;
public sealed class RateMeter
{
    private readonly double _alpha;               // smoothing factor (0..1)
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _count;                          // events since last sample
    private double _ewma;                         // smoothed events/sec
    private long _lastTicks;                      // last sample time (Stopwatch ticks)

    public RateMeter(double alpha = 0.2)
    {
        _alpha = alpha;
        _lastTicks = _sw.ElapsedTicks;
    }

    /// Mark one event.
    public void Tick() => Interlocked.Increment(ref _count);

    /// Call occasionally; updates EWMA at most ~once per second.
    public void MaybeSample()
    {
        long now = _sw.ElapsedTicks;
        long last = Interlocked.Read(ref _lastTicks);         // read once

        long dtTicks = now - last;
        if (dtTicks < Stopwatch.Frequency) return;            // ~1s window

        // Try to claim this sampling window:
        // We expect 'last'; if another thread already sampled, CAS will fail.
        if (Interlocked.CompareExchange(ref _lastTicks, now, last) != last)
            return;

        long c = Interlocked.Exchange(ref _count, 0);
        double dt = (double)dtTicks / Stopwatch.Frequency;
        double inst = dt > 0 ? (c / dt) : 0.0;

        // EWMA smoothing
        double prev = Volatile.Read(ref _ewma);
        double next = (prev == 0.0) ? inst : (_alpha * inst + (1.0 - _alpha) * prev);
        Volatile.Write(ref _ewma, next);
    }

    public double Ewma => Volatile.Read(ref _ewma);
}
