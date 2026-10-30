using System;
using Avalonia.Media;

namespace PLDUOBAO.UI.Effects;

internal static class ColorExtensions
{
    public static Color Lighten(this Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromRgb(
            (byte)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
            (byte)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
            (byte)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));
    }

    public static Color Darken(this Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * (1f - amount), 0, 255),
            (byte)Math.Clamp(color.G * (1f - amount), 0, 255),
            (byte)Math.Clamp(color.B * (1f - amount), 0, 255));
    }
}
