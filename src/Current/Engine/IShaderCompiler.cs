namespace MarcoZechner.CodeDrawDotNet.Engine;

/// <summary>
/// Shader compiler abstraction (e.g., GLSL compile/link).
/// </summary>
internal interface IShaderCompiler
{
    /// <summary>
    /// Compiles and links a shader program from source strings.
    /// </summary>
    /// <param name="vertexSource">Vertex shader source.</param>
    /// <param name="fragmentSource">Fragment shader source.</param>
    /// <returns>Opaque program handle.</returns>
    nint CreateProgram(string vertexSource, string fragmentSource);

    /// <summary>
    /// Destroys a previously created shader program.
    /// </summary>
    /// <param name="program">Program handle to destroy.</param>
    void DestroyProgram(nint program);

    /// <summary>
    /// Gets the location/index of a named uniform in the program, or -1 if absent.
    /// </summary>
    /// <param name="program">Program handle.</param>
    /// <param name="name">Uniform name.</param>
    /// <returns>Uniform location or -1.</returns>
    int GetUniformLocation(nint program, string name);
}