using System.Globalization;

namespace Cheechin;

/// <summary>
/// Convenience extension methods for misc. lower-level dependencies.
/// </summary>
public static class Utility
{
    public static bool IsSameXenotypeAs(this Pawn? pawn, Pawn? other) => pawn?.genes?.Xenotype == other?.genes?.Xenotype;

    public static GeneFurPatternFill? GetGeneFurPatternFill(this Pawn pawn, bool activeCheck = true) => pawn.genes?.GenesListForReading.OfType<GeneFurPatternFill>().FirstOrDefault(p => p.Active || !activeCheck);

    public static GeneFurPatternAccent? GetGeneFurPatternAccent(this Pawn pawn, bool activeCheck = true) => pawn.genes?.GenesListForReading.OfType<GeneFurPatternAccent>().FirstOrDefault(p => p.Active || !activeCheck);

    /// <summary>
    /// Calculate the brightness level (0 to 100) from a color
    /// </summary>
    public static int CalculateBrightnessLevel(this Color color)
    {
        float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        return Mathf.RoundToInt(luminance * 100);
    }

    /// <summary>
    /// Set the color to match the target brightness level (0 to 100)
    /// </summary>
    public static Color WithBrightness(this Color color, int targetBrightnessLevel)
    {
        // Clamp the target brightness level between 0 and 100
        targetBrightnessLevel = Mathf.Clamp(targetBrightnessLevel, 0, 100);

        // Calculate the current brightness level of the original color
        int currentBrightnessLevel = CalculateBrightnessLevel(color);

        // If the current brightness matches the target, return the original color
        if (currentBrightnessLevel == targetBrightnessLevel)
            return color;

        // Normalize the brightness levels to a range of 0 to 1
        float targetLuminance = targetBrightnessLevel / 100f;
        float currentLuminance = currentBrightnessLevel / 100f;

        // Calculate the adjustment factor
        float adjustmentFactor = targetLuminance / currentLuminance;

        // Apply the adjustment to the original color's RGB channels
        Color adjustedColor = new Color(
            color.r * adjustmentFactor,
            color.g * adjustmentFactor,
            color.b * adjustmentFactor,
            color.a // Preserve the original alpha
        );

        // Ensure the adjusted color stays within valid color bounds
        return new(
            Mathf.Clamp01(adjustedColor.r),
            Mathf.Clamp01(adjustedColor.g),
            Mathf.Clamp01(adjustedColor.b),
            adjustedColor.a
        );
    }

    public static bool TryGetColorFromHex(string hex, out Color color)
    {
        color = Color.white;
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);

        if (hex.Length != 6 && hex.Length != 8)
            return false;

        int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
        int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
        int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
        int a = 255;

        if (hex.Length == 8)
            a = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

        color = GenColor.FromBytes(r, g, b, a);
        return true;
    }
}