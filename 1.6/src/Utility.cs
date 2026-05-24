namespace Cheechin;

/// <summary>
/// Convenience extension methods for misc. lower-level dependencies.
/// </summary>
public static class Utility
{
	//public static readonly Color TransparentGrey = new(0.5f,0.5f,0.5f,0f);

	public static bool IsSameXenotypeAs(this Pawn? pawn, Pawn? other) => pawn?.genes?.Xenotype == other?.genes?.Xenotype;

	public static TGene? GetGene<TGene>(this Pawn pawn) where TGene: Gene => pawn.genes?.GenesListForReading.OfType<TGene>().FirstOrDefault(p => p.Active);

	public static FurPatternTheme? GetFurPatternTheme(this Pawn pawn) => pawn.genes.GenesListForReading.FirstOrDefault(g => g.Active && g.def.defName.StartsWith("GeneDef_Cheech_Skin_"))?.def.GetModExtension<FurPatternTheme>();

	/// <summary>
	/// Makes sure a broken or missing gene weight defaults to zero.
	/// </summary>
	private static float AtLeastZero(float weight) => float.IsNaN(weight) || weight < 0f ? 0f : weight;

	/// <summary>
	/// Weighted random pick across <paramref name="genes"/>, with an optional extra "skip" slot of weight <paramref name="skipWeight"/> representing "no item chosen". Returns null if the skip slot wins or genes is empty. Negative or NaN weights are clamped to 0; if every weight is 0, the pick falls back to uniform across <paramref name="genes"/> (amd the skip slot is ignored in that fallback).
	/// </summary>
	public static TGene? SelectByWeight<TGene>(this TGene[] genes, float skipWeight, System.Random random) where TGene: Gene
	{
		float weightTotal = AtLeastZero(skipWeight) + genes.Sum(gene => AtLeastZero(gene.def.selectionWeight));

		if (weightTotal <= 0f)
			return genes.Length == 0 ? null : genes[random.Next(genes.Length)];

		double r = random.NextDouble() * weightTotal;

		foreach (var gene in genes)
		{
			r -= AtLeastZero(gene.def.selectionWeight);
			if (r < 0)
				return gene;
		}

		// If we get here, skip slot won (or rounding pushed us past the end).
		return null;
	}


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

	public static Color ParseDefColor(string s)
	{
		s = s.Trim();

		if (s.StartsWith("#"))
			s = s.Substring(1);

		if (s.Length is 6 or 8 && s.All(Uri.IsHexDigit))
			return new(
				Convert.ToInt32(s.Substring(0, 2), 16) / 255f,
				Convert.ToInt32(s.Substring(2, 2), 16) / 255f,
				Convert.ToInt32(s.Substring(4, 2), 16) / 255f,
				s.Length == 8 ? Convert.ToInt32(s.Substring(6, 2), 16) / 255f : 1f
			);

		return ParseHelper.FromString<Color>(s);
	}

	public static Color ColorWithAlpha(Color color, Color alpha) => new(color.r, color.g, color.b, alpha.a);

	public static Color Coalesce(string? def, Color fallbackColor, Color? fallbackAlpha) => string.IsNullOrWhiteSpace(def)
		? fallbackAlpha == null
			? fallbackColor
			: ColorWithAlpha(fallbackColor, fallbackAlpha.Value)
		: ParseDefColor(def!);

	public static Color Average(Color low, Color high) => new((low.r + high.r) * 0.5f, (low.g + high.g) * 0.5f, (low.b + high.b) * 0.5f, (low.a + high.a) * 0.5f);

	public static Color RandomBetween(Color low, Color high) => new(Rand.Range(low.r, high.r), Rand.Range(low.g, high.g), Rand.Range(low.b, high.b), Rand.Range(low.a, high.a));
}