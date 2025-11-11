namespace MarcoZechner.CodeDrawDotNet.Engine.Abstractions;

/// <summary>
/// Shared layer renderer. Executes layer command buffers and publishes triple-buffer outputs.
/// </summary>
internal interface ILayerRenderer
{
    /// <summary>
    /// Queues a layer for rendering this tick, providing its command queue and target.
    /// </summary>
    /// <typeparam name="T">Command struct type.</typeparam>
    /// <param name="layerId">Stable layer identifier.</param>
    /// <param name="commands">Recorded commands to execute.</param>
    /// <param name="target">Render target to receive the output.</param>
    void EnqueueLayer<T>(Guid layerId, ICommandQueue<T> commands, IRenderTarget target) where T : struct;

    /// <summary>
    /// Performs one render tick: fires the Update event, renders all enqueued layers, and publishes outputs.
    /// </summary>
    /// <param name="deltaTime">Time in seconds since the previous tick.</param>
    void RenderTick(double deltaTime);
}