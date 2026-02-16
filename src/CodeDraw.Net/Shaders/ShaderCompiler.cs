using System.Text;
using System.Text.RegularExpressions;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.Shaders;

public static class ShaderCompiler
{
    private static void ThrowIfNonAscii(string src, string label)
    {
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] <= 0x7F) continue;
            ThrowWithContext(src, label, i, $"non-ASCII char U+{(int)src[i]:X4}");
        }
    }

    private static void ThrowIfNul(string src, string label)
    {
        for (int i = 0; i < src.Length; i++)
        {
            if (src[i] != '\0') continue;
            ThrowWithContext(src, label, i, "NUL (\\0) character");
        }
    }

    private static void ThrowWithContext(string src, string label, int index, string what)
    {
        var (line, col) = ComputeLineCol(src, index);
        var ctx = BuildContext(src, line, col, radius: 4);

        throw new Exception(
            $"Shader '{label}' contains {what} at line {line}, col {col}.\n{ctx}");
    }
    
    // ============================================================
    // Context building
    // ============================================================

    private static (int line, int col) ComputeLineCol(string src, int index)
    {
        index = Math.Clamp(index, 0, Math.Max(0, src.Length - 1));

        int line = 1, col = 1;
        for (int k = 0; k < index; k++)
        {
            if (src[k] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }

    private static string BuildContext(string src, int? line = null, int? col = null, int radius = 6)
    {
        var lines = src.Replace("\r\n", "\n").Split('\n');

        if (line is null)
            return BuildHead(lines, 12);

        int target = Math.Clamp(line.Value, 1, Math.Max(1, lines.Length));
        int start = Math.Max(1, target - radius);
        int end = Math.Min(lines.Length, target + radius);

        var sb = new StringBuilder();
        sb.AppendLine($"--- Shader context ({start}..{end} of {lines.Length}) ---");

        for (int i = start; i <= end; i++)
        {
            bool isTarget = (i == target);
            var prefix = isTarget ? ">>" : "  ";

            sb.Append(prefix);
            sb.Append($"{i,4}: ");
            sb.AppendLine(lines[i - 1]);

            if (isTarget && col.HasValue)
            {
                // indent aligns with "  ####: "
                sb.Append("      "); // 2 for prefix + 4 digits
                sb.Append("  ");     // ": "
                for (int c = 1; c < col.Value; c++) sb.Append(' ');
                sb.AppendLine("^");
            }
        }

        return sb.ToString();
    }

    private static string BuildHead(string[] lines, int maxLines = 12)
    {
        var n = Math.Min(maxLines, lines.Length);
        var sb = new StringBuilder();
        sb.AppendLine($"--- Shader head ({n} of {lines.Length} lines) ---");
        for (var i = 0; i < n; i++)
            sb.AppendLine($"{i + 1,4}: {lines[i]}");
        return sb.ToString();
    }
    
    // ============================================================
    // GLSL compilation
    // ============================================================


    private static uint CreateShader(GL gl, GLEnum type, string src, string label)
    {
        var s = gl.CreateShader(type);

        // Preflight: catch the “premature EOF” family of pain early.
        ThrowIfNul(src, label);
        ThrowIfNonAscii(src, label);

        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok != 0) return s;

        var log = gl.GetShaderInfoLog(s);
        Console.WriteLine(log);
        gl.DeleteShader(s);

        var lineNr = TryExtractLine(log);

        if (lineNr is { } ln)
            throw new Exception(
                $"Shader compile failed [{label}] ({type}): {log}\n{BuildContext(src, ln)}");

        throw new Exception(
            $"Shader compile failed [{label}] ({type}): {log}\n(No line number found.)\n{BuildContext(src)}");

        // Try to extract a GLSL line number from common driver formats:
        //  - "ERROR: 0:57: ...", "WARNING: 0:12: ..."
        //  - "0(57) : error C0000: ..."
        static int? TryExtractLine(string compileLog)
        {
            var m = Regex.Match(compileLog, @"\b\d+:(\d+):");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var ln)) return ln;

            m = Regex.Match(compileLog, @"\b\d+\((\d+)\)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out ln)) return ln;

            return null;
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
