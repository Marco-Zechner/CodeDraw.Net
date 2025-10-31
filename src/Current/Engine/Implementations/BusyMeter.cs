using System.Diagnostics;

namespace MarcoZechner.CodeDrawDotNet.Engine;

internal sealed class BusyMeter
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastTicks;       // last sample time
    private long _busyTicks;       // accumulated busy ticks since last sample
    private double _dutyEwma;      // smoothed busy fraction
    private readonly double _alpha;

    public BusyMeter(double alpha = 0.25)
    {
        _alpha = alpha;
        _lastTicks = _sw.ElapsedTicks;
    }

    public IDisposable Scope()
    {
        var start = _sw.ElapsedTicks;
        return new ScopeImpl(this, start);
    }

    private void AddBusy(long ticks) => Interlocked.Add(ref _busyTicks, ticks);

    private sealed class ScopeImpl(BusyMeter m, long start) : IDisposable
    {
        private readonly BusyMeter _m = m;
        private readonly long _start = start;
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            long end = _m._sw.ElapsedTicks;
            _m.AddBusy(end - _start);
        }
    }

    public void MaybeSample()
    {
        long now = _sw.ElapsedTicks;
        long last = Interlocked.Read(ref _lastTicks);
        long dtTicks = now - last;
        if (dtTicks < Stopwatch.Frequency) return; // ~1s cadence

        if (Interlocked.CompareExchange(ref _lastTicks, now, last) != last) return;

        long busy = Interlocked.Exchange(ref _busyTicks, 0);
        double dt = (double)dtTicks / Stopwatch.Frequency;
        double duty = dt > 0 ? (busy / (double)Stopwatch.Frequency) / dt : 0.0;
        double prev = Volatile.Read(ref _dutyEwma);
        double next = (prev == 0.0) ? duty : (_alpha * duty + (1 - _alpha) * prev);
        Volatile.Write(ref _dutyEwma, next);
    }

    public double Duty => Volatile.Read(ref _dutyEwma); // 0..1
}
