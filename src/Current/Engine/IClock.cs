namespace MarcoZechner.CodeDrawDotNet.Engine;

/// <summary>
/// Monotonic clock provider for dt/uptime measurements.
/// </summary>
internal interface IClock
{
    /// <summary>
    /// Returns a strictly monotonic timestamp suitable for computing deltas.
    /// </summary>
    /// <returns>Timestamp in seconds.</returns>
    double NowSeconds();

    /// <summary>
    /// Returns the duration since the given timestamp, in seconds.
    /// </summary>
    /// <param name="sinceSeconds">Reference timestamp in seconds.</param>
    /// <returns>Elapsed time in seconds.</returns>
    double Elapsed(double sinceSeconds);
}