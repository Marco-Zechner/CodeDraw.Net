using MarcoZechner.CodeDrawDotNet.Text;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal sealed class CmdText : ICmd
{
    public string Text = "";
    public float X, Y;
    public TextStyle Style = null!;
    public Matrix3x3 Xf;
    public void Exec(GL gl, CodeDrawLayer self) => self.ExecText(gl, Text, X, Y, Style, Xf);
}