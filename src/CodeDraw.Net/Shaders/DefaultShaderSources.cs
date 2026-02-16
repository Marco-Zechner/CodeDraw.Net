namespace MarcoZechner.CodeDrawDotNet.Shaders;

/// <summary>
/// ShaderStore-only helper: minimal built-in shaders so missing files do NOT brick rendering.
/// Keep these *compatible* with your uniforms/attributes.
/// </summary>
public static class DefaultShaderSources
{
    // Fullscreen quad layout assumed:
    // location 0: vec2 aPos
    // location 1: vec2 aUv

    public static bool TryGet(ShaderKey key, out string vs, out string fs)
    {
        var vName = Path.GetFileName(key.VertBaseAbs);

        switch (vName)
        {
            case "layerShader":
                vs = VS_FULLSCREEN_UV;
                fs = FS_BLIT_U_TEX;
                return true;

            case "rect":
                vs = VS_RECT_POS_SIZE_RES;
                fs = FS_SOLID_U_COLOR;
                return true;

            case "layerRectShader":
                vs = VS_FULLSCREEN_UV;
                fs = FS_BLIT_RECT_U_TEX;
                return true;

            default:
                // generic: blit uTex
                vs = VS_FULLSCREEN_UV;
                fs = FS_BLIT_U_TEX;
                return true;
        }
    }

    private const string VS_FULLSCREEN_UV = """
        #version 450
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUv;
        layout(location=0) out vec2 vUv;
        void main() {
            vUv = aUv;
            gl_Position = vec4(aPos, 0.0, 1.0);
        }
        """;

    // Matches your rect shader usage:
    // Uniform4 uPosSize (x,y,w,h) in pixels
    // Uniform2 uRes (w,h) in pixels
    private const string VS_RECT_POS_SIZE_RES = """
        #version 450
        layout(location=0) in vec2 aPos;
        layout(location=1) in vec2 aUv;

        layout(location=0) out vec2 vUv;

        uniform vec4 uPosSize; // x,y,w,h in px
        uniform vec2 uRes;     // target size in px

        void main() {
            // aPos is [-1..1] fullscreen; convert to [0..1] quad local
            vec2 q = aUv; // 0..1
            vec2 px = uPosSize.xy + q * uPosSize.zw;

            // pixel -> NDC
            vec2 ndc = (px / uRes) * 2.0 - 1.0;
            // flip Y if your screen-space uses top-left origin; adjust if needed
            ndc.y = -ndc.y;

            vUv = aUv;
            gl_Position = vec4(ndc, 0.0, 1.0);
        }
        """;

    private const string FS_SOLID_U_COLOR = """
        #version 450
        layout(location=0) out vec4 outColor;
        uniform vec4 uColor;
        void main() {
            outColor = uColor;
        }
        """;

    private const string FS_BLIT_U_TEX = """
        #version 450
        layout(location=0) in vec2 vUv;
        layout(location=0) out vec4 outColor;
        uniform sampler2D uTex;
        void main() {
            outColor = texture(uTex, vUv);
        }
        """;

    // Matches your layerRectShader usage:
    // uDstRectPx: x,y,w,h in px
    // uDstResPx:  w,h in px
    // uSrcUvRect: u0,v0,du,dv
    private const string FS_BLIT_RECT_U_TEX = """
        #version 450
        layout(location=0) in vec2 vUv;
        layout(location=0) out vec4 outColor;

        uniform sampler2D uTex;
        uniform vec4 uDstRectPx;
        uniform vec2 uDstResPx;
        uniform vec4 uSrcUvRect;

        void main() {
            // vUv is fullscreen [0..1] from quad
            // Check if within destination rect (pixel space)
            vec2 px = vec2(vUv.x * uDstResPx.x, (1.0 - vUv.y) * uDstResPx.y); // vUv.y top->bottom to px top->bottom
            vec2 p0 = uDstRectPx.xy;
            vec2 p1 = uDstRectPx.xy + uDstRectPx.zw;

            if (px.x < p0.x || px.y < p0.y || px.x > p1.x || px.y > p1.y) {
                discard;
            }

            // Map to src uv rect
            vec2 local = (px - p0) / (p1 - p0);
            vec2 uv = uSrcUvRect.xy + local * uSrcUvRect.zw;

            outColor = texture(uTex, uv);
        }
        """;
}
