namespace Cheechin;

/// <summary>
/// Convenience extension methods for misc. lower-level dependencies.
/// </summary>
public static class Utility
{
    public static readonly Color TransparentGray = new(0.5f,0.5f,0.5f,0f);

    public static bool IsSameXenotypeAs(this Pawn? pawn, Pawn? other) => pawn?.genes?.Xenotype == other?.genes?.Xenotype;

    public static TGene? GetGene<TGene>(this Pawn pawn) where TGene: Gene => pawn.genes?.GenesListForReading.OfType<TGene>().FirstOrDefault(p => p.Active);

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

    public static int ToDefVal(this float c) => Mathf.RoundToInt(c * 255f);

    public static string ToDefVal(this Color color) => color.a.ToDefVal() < 255
        ? $"({color.r.ToDefVal()},{color.g.ToDefVal()},{color.b.ToDefVal()},{color.a.ToDefVal()})"
        : $"({color.r.ToDefVal()},{color.g.ToDefVal()},{color.b.ToDefVal()})"
    ;

}