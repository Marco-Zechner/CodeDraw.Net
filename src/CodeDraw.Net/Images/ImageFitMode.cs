namespace MarcoZechner.CodeDrawDotNet.Images;

public enum ImageFitMode
{
    Fit,     // stretch
    Contain, // preserve aspect, letterbox
    Cover,   // preserve aspect, crop
    PixelPerfect,
    Repeat
}