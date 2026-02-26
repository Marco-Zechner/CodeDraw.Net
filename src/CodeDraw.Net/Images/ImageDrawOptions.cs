namespace MarcoZechner.CodeDrawDotNet.Images;

public readonly record struct ImageDrawOptions(
    ImageFitMode FitMode = ImageFitMode.Fit,
    ImageAnchor Anchor = ImageAnchor.Center,
    bool FlipY = false,
    float RepeatScale = 1f // used by Repeat
);