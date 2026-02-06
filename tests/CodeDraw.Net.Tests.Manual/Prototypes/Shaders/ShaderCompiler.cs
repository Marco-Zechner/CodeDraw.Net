using System.Text.RegularExpressions;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.Shaders;

public static class ShaderCompiler
{

    private static uint CreateShader(GL gl, GLEnum type, string src, string label)
    {
        var s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok != 0) return s;

        var log = gl.GetShaderInfoLog(s);
        Console.WriteLine(log);
        gl.DeleteShader(s);

        var lineNr = TryExtractLine(log);
        if (lineNr is { } ln)
            throw new Exception($"Shader compile failed [{label}] ({type}): {log}\n{DumpAround(src, ln, 8)}");

        throw new Exception($"Shader compile failed [{label}] ({type}): {log}\n(No line number found.)\n{Head(src)}");

        // Try to extract a GLSL line number from common driver formats:
        //  - "ERROR: 0:57: ...", "WARNING: 0:12: ..."
        //  - "0(57) : error C0000: ..."
        //  - "0:57( ... )" etc.
        static int? TryExtractLine(string compileLog)
        {
            var m = Regex.Match(compileLog, @"\b\d+:(\d+):");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var ln)) return ln;

            m = Regex.Match(compileLog, @"\b\d+\((\d+)\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out ln)) return ln;

            return null;
        }

        static string DumpAround(string shader, int line, int radius = 6)
        {
            // Normalize newlines for consistent line counts
            var lines = shader.Replace("\r\n", "\n").Split('\n');

            // Clamp target line
            var target = Math.Clamp(line, 1, Math.Max(1, lines.Length));

            var start = Math.Max(1, target - radius);
            var end   = Math.Min(lines.Length, target + radius);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"--- Shader excerpt (lines {start}..{end} of {lines.Length}) ---");

            for (var i = start; i <= end; i++)
            {
                var prefix = Math.Abs(i - target) switch
                {
                    0 => ">>",
                    1 => "> ",
                    _ => "  "
                };
                sb.Append(prefix);
                sb.Append($"{i,4}: ");
                sb.AppendLine(lines[i - 1]);
            }

            return sb.ToString();
        }

        // No usable line number (e.g., 0:? or weird log) -> keep it short, no full dump.
        // Still include a tiny head so you can catch the usual "#version not first" issue.
        static string Head(string shader, int maxLines = 12)
        {
            var lines = shader.Replace("\r\n", "\n").Split('\n');
            var n = Math.Min(maxLines, lines.Length);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"--- Shader head ({n} of {lines.Length} lines) ---");
            for (var i = 0; i < n; i++)
                sb.AppendLine($"{i + 1,4}: {lines[i]}");
            return sb.ToString();
        }
    }

    public static uint CreateProgram(GL gl, string vs, string fs, string label = "Unknown")
    {
        var v = CreateShader(gl, GLEnum.VertexShader, vs, label + ".vert");
        var f = CreateShader(gl, GLEnum.FragmentShader, fs, label + ".frag");

        var p = gl.CreateProgram();
        gl.AttachShader(p, v);
        gl.AttachShader(p, f);
        gl.LinkProgram(p);
        gl.GetProgram(p, GLEnum.LinkStatus, out var ok);

        gl.DeleteShader(v);
        gl.DeleteShader(f);

        if (ok != 0) return p;

        var log = gl.GetProgramInfoLog(p);
        gl.DeleteProgram(p);
        throw new Exception($"Program link failed [{label}]: {log}");
    }

    public static unsafe (uint vao, uint vbo, uint ebo) CreateFullScreenQuad(GL gl)
    {
        // pos(x,y) uv(u,v)
        float[] verts =
        [
            -1, -1, 0, 0,
             1, -1, 1, 0,
             1,  1, 1, 1,
            -1,  1, 0, 1,
        ];
        uint[] idx = [0, 1, 2, 0, 2, 3];

        var vao = gl.GenVertexArray();
        var vbo = gl.GenBuffer();
        var ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (float* p = verts)
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), p, GLEnum.StaticDraw);

        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        fixed (uint* p = idx)
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(idx.Length * sizeof(uint)), p, GLEnum.StaticDraw);

        // location 0: vec2 pos
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)0);

        // location 1: vec2 uv
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        gl.BindVertexArray(0);
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

        return (vao, vbo, ebo);
    }
}
