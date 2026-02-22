using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;

internal interface ICmd { void Exec(GL gl, CodeDrawLayer self); }