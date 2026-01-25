using System.Collections.Concurrent;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Engine;

public static class Gl2DResources
{
    private sealed class Res
    {
        public uint Vao;
        public uint Vbo;
        public uint Program2D;
        public int  LocViewport; // uniform location for u_viewport (w,h)
    }

    private static readonly ConcurrentDictionary<nint, Res> _map = new();

    // IMPORTANT: call this on the render thread while THIS window's context is current.
    public static unsafe void Install(GL gl, nint window)
    {
        // If already installed (e.g. recreate renderer), uninstall first (defensive)
        if (_map.TryGetValue(window, out var old))
        {
            Uninstall(gl, window);
        }

        // --- 1) Create persistent VAO/VBO ---
        var vao = gl.GenVertexArray();
        var vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        // Attribute layout for interleaved [x,y,r,g,b,a] floats
        uint stride = (uint)((2 + 4) * sizeof(float));

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        gl.BindVertexArray(0);

        // --- 2) Compile program that uses the above attribute locations ---
        const string vs = """
            #version 330 core
            layout(location=0) in vec2 a_posPx;  // pixel coords, top-left origin (your API)
            layout(location=1) in vec4 a_col;

            uniform vec2 u_viewport;             // (w,h) in pixels

            out vec4 v_col;

            void main()
            {
                // Convert pixel coords (top-left) to NDC (-1..1, +Y up)
                vec2 ndc;
                ndc.x =  (a_posPx.x / u_viewport.x) * 2.0 - 1.0;
                ndc.y = -(a_posPx.y / u_viewport.y) * 2.0 + 1.0;

                gl_Position = vec4(ndc, 0.0, 1.0);
                v_col = a_col;
            }
            """;

        const string fs = """
            #version 330 core
            in vec4 v_col;
            out vec4 o;
            void main()
            {
                o = v_col;
            }
            """;

        uint prog = CompileProgram(gl, vs, fs);
        int locViewport = gl.GetUniformLocation(prog, "u_viewport");
        if (locViewport < 0)
        {
            // If this happens, the uniform got optimized out (shouldn't), but fail loudly.
            gl.DeleteProgram(prog);
            gl.DeleteBuffer(vbo);
            gl.DeleteVertexArray(vao);
            throw new InvalidOperationException("2D shader missing uniform u_viewport (optimized out or name mismatch).");
        }

        _map[window] = new Res
        {
            Vao = vao,
            Vbo = vbo,
            Program2D = prog,
            LocViewport = locViewport,
        };
    }

    // IMPORTANT: call this on the render thread while THIS window's context is current.
    public static void Uninstall(GL gl, nint window)
    {
        if (!_map.TryRemove(window, out var r)) return;

        if (r.Program2D != 0) gl.DeleteProgram(r.Program2D);
        if (r.Vbo != 0) gl.DeleteBuffer(r.Vbo);
        if (r.Vao != 0) gl.DeleteVertexArray(r.Vao);
    }

    public static (uint vao, uint vbo, uint program2D, int locViewport) Get(nint window)
    {
        if (!_map.TryGetValue(window, out var r))
            throw new InvalidOperationException($"Gl2DResources not installed for window {window}");
        return (r.Vao, r.Vbo, r.Program2D, r.LocViewport);
    }

    private static uint CompileProgram(GL gl, string vsSrc, string fsSrc)
    {
        uint vs = Compile(gl, ShaderType.VertexShader, vsSrc);
        uint fs = Compile(gl, ShaderType.FragmentShader, fsSrc);

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);

        gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = gl.GetProgramInfoLog(p);
            gl.DeleteProgram(p);
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            throw new Exception($"2D Program link failed:\n{log}");
        }

        gl.DetachShader(p, vs);
        gl.DetachShader(p, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);

        return p;
    }

    private static uint Compile(GL gl, ShaderType type, string src)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);

        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = gl.GetShaderInfoLog(s);
            gl.DeleteShader(s);
            throw new Exception($"{type} compile failed:\n{log}");
        }
        return s;
    }
}
