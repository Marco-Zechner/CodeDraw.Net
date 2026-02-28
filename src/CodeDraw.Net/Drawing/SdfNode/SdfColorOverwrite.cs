namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

public enum SdfColorOverwrite
{
    Everything = 0,  // parent forces material for entire subtree
    OnlyDefault = 1, // parent material is fallback; child material replaces it
}