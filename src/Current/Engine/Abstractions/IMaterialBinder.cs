namespace MarcoZechner.CodeDrawDotNet.Engine.Abstractions;
/// <summary>
/// Material binder: applies uniform values/samplers for a material prior to drawing.
/// </summary>
internal interface IMaterialBinder
{
    /// <summary>
    /// Binds the program and applies all pending uniform/sampler updates for the material.
    /// </summary>
    /// <param name="material">Engine material object.</param>
    void Bind(object material);

    /// <summary>
    /// Sets/updates a uniform value for a material (overloaded by type in implementation).
    /// </summary>
    /// <param name="material">Material object.</param>
    /// <param name="name">Uniform name.</param>
    /// <param name="value">Boxed value (float, Vector2, Vector3, Vector4, Matrix3x2/Matrix4x4, int, etc.).</param>
    void SetUniform(object material, string name, object value);
}