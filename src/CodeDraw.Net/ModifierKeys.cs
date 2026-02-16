using Silk.NET.GLFW;

namespace MarcoZechner.CodeDrawDotNet;

[Flags]
public enum ModifierKeys
{
    /// <summary>
    /// None of the modifier keys were held down.
    /// </summary>
    NONE = 0x0000,

    /// <summary>
    /// if one or more Shift keys were held down.
    /// </summary>
    SHIFT = 0x0001,

    /// <summary>
    /// If one or more Control keys were held down.
    /// </summary>
    CONTROL = 0x0002,

    /// <summary>
    /// If one or more Alt keys were held down.
    /// </summary>
    ALT = 0x0004,

    /// <summary>
    /// If one or more Super keys were held down.
    /// </summary>
    SUPER = 0x0008,

    /// <summary>
    /// If the Caps Lock key is enabled.
    /// </summary>
    CAPS_LOCK = 0x0010,

    /// <summary>
    /// If the Num Lock key is enabled.
    /// </summary>
    NUM_LOCK = 0x0020
}

public static class ModifierKeysMethods
{
    public static Keys[] ToKeys(this ModifierKeys mods)
    {
        HashSet<Keys> keys = [];
        if (mods.HasFlag(ModifierKeys.SHIFT))
        {
            keys.Add(Keys.ShiftLeft);
            keys.Add(Keys.ShiftRight);
        }
        if (mods.HasFlag(ModifierKeys.CONTROL))
        {
            keys.Add(Keys.ControlLeft);
            keys.Add(Keys.ControlRight);
        }
        if (mods.HasFlag(ModifierKeys.ALT))
        {
            keys.Add(Keys.AltLeft);
            keys.Add(Keys.AltRight);
        }
        if (mods.HasFlag(ModifierKeys.SUPER))
        {
            keys.Add(Keys.SuperLeft);
            keys.Add(Keys.SuperRight);
        }
        if (mods.HasFlag(ModifierKeys.CAPS_LOCK))
        {
            keys.Add(Keys.CapsLock);
        }
        if (mods.HasFlag(ModifierKeys.NUM_LOCK))
        {
            keys.Add(Keys.NumLock);
        }
        return keys.ToArray();
    }
}