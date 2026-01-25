namespace MarcoZechner.CodeDrawDotNet.Engine;

internal static class Shaders2D
{
    public const string VERTEX = """
                                 #version 330 core
                                 layout(location=0) in vec2 aPosPx;
                                 layout(location=1) in vec4 aColor;

                                 out vec4 vColor;

                                 uniform vec2 uViewport; // (w,h)

                                 void main()
                                 {
                                     vec2 p = aPosPx / uViewport;                 // 0..1
                                     vec2 ndc = vec2(p.x * 2.0 - 1.0, 1.0 - p.y * 2.0); // -1..1 with top-left origin
                                     gl_Position = vec4(ndc, 0.0, 1.0);
                                     vColor = aColor;
                                 }
                                 """;

    public const string FRAGMENT = """
                                   #version 330 core
                                   in vec4 vColor;
                                   out vec4 FragColor;
                                   void main()
                                   {
                                       FragColor = vColor;
                                   }
                                   """;
}