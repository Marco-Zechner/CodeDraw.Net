using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Test3;

public class Test3_SharedContext
{
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private SharedLayer _sharedLayer = null!;

    private async Task SetupSharedLayer()
    {
        var mgr = SharedGlManager.Instance;

        _sharedLayer = await mgr.CreateLayerAsync(512, 512);

        _ = Task.Run(async () =>
        {
            double t = 0;
            while (!_cts.IsCancellationRequested)
            {
                t += 0.2;
                float r = 0.5f + 0.5f * MathF.Sin((float)t);
                float g = 0.5f + 0.5f * MathF.Sin((float)t + 2.0f);
                float b = 0.5f + 0.5f * MathF.Sin((float)t + 4.0f);

                await mgr.DrawIntoAsync(_sharedLayer, gl =>
                {
                    gl.ClearColor(r, g, b, 1f);
                    gl.Clear((uint)ClearBufferMask.ColorBufferBit);
                });

                await Task.Delay(200);
            }
        });
    }

    public void Run()
    {
        SetupSharedLayer().GetAwaiter().GetResult();
        
        var tA = new Thread(() => RunWindow("A (shared texture)", 800, 600, _sharedLayer)) { IsBackground = true };
        var tB = new Thread(() => RunWindow("B (shared texture)", 800, 600, _sharedLayer)) { IsBackground = true };

        tA.Start();
        tB.Start();

        // wait until both windows close
        tA.Join();
        tB.Join();

        // stop updater and exit
        _cts.Cancel();
    }
    
    static unsafe void RunWindow(string title, int w, int h, SharedLayer layer)
    {
        var mgr = SharedGlManager.Instance;
        var share = mgr.Acquire();
        var glfw = mgr.Glfw;

        mgr.ApplyWindowHints();
        var win = glfw.CreateWindow(w, h, title, null, share);
        if (win == null) throw new Exception("CreateWindow failed");

        glfw.MakeContextCurrent(win);
        var gl = GL.GetApi(glfw.GetProcAddress);

        try
        {
            var ver = gl.GetStringS(GLEnum.Version);
            var ven = gl.GetStringS(GLEnum.Vendor);
            var ren = gl.GetStringS(GLEnum.Renderer);
            Logger.LogLine($"[{title}] context: {ver} | {ven} | {ren}");
        }
        catch { /* ignore */ }

        gl.Enable(GLEnum.DebugOutput);
        gl.Enable(GLEnum.DebugOutputSynchronous);
        unsafe {
        gl.DebugMessageCallback((source, type, id, severity, length, message, userparam) => {
            string msg = Marshal.PtrToStringAnsi(message, length);
            Console.Error.WriteLine($"[DebugMessageCallback] source: {source}, type: {type}, id: {id}, severity {severity}, length {length}, userParam {userparam}\n{msg}\n\n");
        }, (void*) 0);
        }

        uint prog = GLHelpers.CreateProgram(gl, GLHelpers.VS, GLHelpers.FS);
        var (vao, vbo, ebo) = GLHelpers.CreateFullScreenQuad(gl);

        gl.Enable(GLEnum.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

        Stopwatch stopwatch = new Stopwatch();

        while (!glfw.WindowShouldClose(win))
        {
            var time = stopwatch.ElapsedMilliseconds;

            if (time < 1)
            {
                Thread.Sleep((int)(1 - time));
            }

            stopwatch.Restart();
            glfw.PollEvents();

            glfw.GetFramebufferSize(win, out var fbW, out var fbH);
            gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
            gl.ClearColor(0.08f, 0.08f, 0.09f, 1f);
            gl.Clear((uint)ClearBufferMask.ColorBufferBit);

            // sample the shared texture
            layer.BeginUse();
            gl.UseProgram(prog);
            gl.BindVertexArray(vao);

            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, layer.Texture);
            gl.Uniform1(gl.GetUniformLocation(prog, "uTex"), 0);

            gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            layer.EndUse();

            glfw.SwapBuffers(win);
            stopwatch.Stop();
        }

        // cleanup
        gl.DeleteVertexArray(vao);
        gl.DeleteBuffer(vbo);
        gl.DeleteBuffer(ebo);
        gl.DeleteProgram(prog);

        glfw.DestroyWindow(win);
        mgr.Release();
    }
}

internal class GLHelpers
{
    public static uint CreateShader(GL gl, GLEnum type, string src)
    {
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, GLEnum.CompileStatus, out int ok);
        if (ok == 0) { var log = gl.GetShaderInfoLog(s); throw new Exception($"Shader compile failed: {log}"); }
        return s;
    }

    public static uint CreateProgram(GL gl, string vs, string fs)
    {
        uint v = CreateShader(gl, GLEnum.VertexShader, vs);
        uint f = CreateShader(gl, GLEnum.FragmentShader, fs);
        uint p = gl.CreateProgram();
        gl.AttachShader(p, v); gl.AttachShader(p, f);
        gl.LinkProgram(p);
        gl.GetProgram(p, GLEnum.LinkStatus, out int ok);
        gl.DeleteShader(v); gl.DeleteShader(f);
        if (ok == 0) { var log = gl.GetProgramInfoLog(p); throw new Exception($"Program link failed: {log}"); }
        return p;
    }

    public unsafe static (uint vao, uint vbo, uint ebo) CreateFullScreenQuad(GL gl)
    {
        // pos (x,y) in NDC, uv (u,v)
        float[] verts = [
            -1, -1, 0, 0,
            1, -1, 1, 0,
            1,  1, 1, 1,
            -1,  1, 0, 1,
        ];
        uint[] idx = [0, 1, 2, 0, 2, 3];

        gl.CreateVertexArrays(1, out uint vao);
        gl.CreateBuffers(1, out uint vbo);
        gl.CreateBuffers(1, out uint ebo);

        fixed (float* p = verts)
        {
            gl.NamedBufferData(vbo, (nuint)(verts.Length * sizeof(float)), p, GLEnum.StaticDraw);
        }
        fixed (uint* p = idx)
        {
            gl.NamedBufferData(ebo, (nuint)(idx.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        }

        gl.VertexArrayVertexBuffer(vao, 0, vbo, 0, 4 * sizeof(float));
        gl.EnableVertexArrayAttrib(vao, 0);
        gl.EnableVertexArrayAttrib(vao, 1);
        gl.VertexArrayAttribFormat(vao, 0, 2, GLEnum.Float, false, 0);
        gl.VertexArrayAttribFormat(vao, 1, 2, GLEnum.Float, false, 2 * sizeof(float));
        gl.VertexArrayAttribBinding(vao, 0, 0);
        gl.VertexArrayAttribBinding(vao, 1, 0);

        gl.VertexArrayElementBuffer(vao, ebo);
        return (vao, vbo, ebo);
    }
    
    public const string VS = @"
    #version 330 core
    layout(location=0) in vec2 aPos;
    layout(location=1) in vec2 aUV;
    out vec2 vUV;
    void main() {
        vUV = aUV;
        gl_Position = vec4(aPos, 0.0, 1.0);
    }";

    public const string FS = @"
    #version 330 core
    in vec2 vUV;
    out vec4 FragColor;
    uniform sampler2D uTex;
    void main() {
        FragColor = texture(uTex, vUV);
    }";
}