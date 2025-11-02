namespace MarcoZechner.CodeDrawDotNet.Engine.Abstractions;

/// <summary>
/// Single-producer/single-consumer lock-free ring buffer for draw commands.
/// </summary>
/// <typeparam name="T">Command struct type.</typeparam>
internal interface ICommandQueue<T> where T : struct
{
    /// <summary>
    /// Attempts to enqueue a command. Returns false if the queue is full.
    /// </summary>
    /// <param name="cmd">Command to enqueue.</param>
    /// <returns>True if enqueued; otherwise false.</returns>
    bool TryEnqueue(in T cmd);

    /// <summary>
    /// Attempts to dequeue the next command. Returns false if the queue is empty.
    /// </summary>
    /// <param name="cmd">Output command if available.</param>
    /// <returns>True if dequeued; otherwise false.</returns>
    bool TryDequeue(out T cmd);

    /// <summary>
    /// Clears the queue, discarding all pending commands.
    /// </summary>
    void Clear();
}