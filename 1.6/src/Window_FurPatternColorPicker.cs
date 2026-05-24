namespace Cheechin;

/// <summary>
/// This code is a hideous mutation based on Anthrosonae mod's color picker.
/// </summary>
[StaticConstructorOnStartup]
public sealed class Window_FurPatternColorPicker: Window
{
	private const int Skin = 0;
	private const int BodyFill = 1;
	private const int BodyAccent = 2;
	private const int HeadFill = 3;
	private const int HeadAccent = 4;
	private const int EarsFill = 5;
	private const int EarsAccent = 6;
	private const int TailFill = 7;
	private const int TailAccent = 8;
	private const int ColorCount = 9;

	private readonly Color[] originalColors = new Color[ColorCount];

	private readonly Color[] selectedColors = new Color[ColorCount];

	private readonly string[] selectedColorsAsDefVals = new string[ColorCount];

	private readonly float[] selectedColorsLuminosity = new float[ColorCount];

	private string[] selectedColorsLuminosityAsString = new string[ColorCount];

	private int selectedPattern;

	private readonly GeneFurPattern?[] geneFurPatterns = new GeneFurPattern[ColorCount];

	private bool hsvColorWheelDragging;

	private readonly string[][] textFieldBuffers = [new string[6],new string[6],new string[6],new string[6],new string[6],new string[6],new string[6],new string[6],new string[6]];

	private readonly Color[] textFieldColorBuffers = new Color[ColorCount];

	private string? previousFocusedControlName;

	public const Widgets.ColorComponents hueAndSatFields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;

	public override Vector2 InitialSize => new(800f, 410f);

	private readonly FurPatternTheme[][] predefinedPatternColors = new FurPatternTheme[ColorCount][];

	private Rot4 pawnFacingDirection = Rot4.South;

	private readonly Pawn pawn;

	/// <summary>
	/// This color picker class started to get ugly when we use colorTwo in some cases and not others. Adam swears he doesn't normally write code this weird. He just didn't want to refactor it all just yet.
	/// </summary>
	private static ref Color Rt(int i, GeneFurPattern[] g) => ref (i is EarsAccent or TailAccent ? ref g[i].colorTwo : ref g[i].colorOne);

	/// <inheritdoc cref="Rt"/>
	private static Color Ot(int i, FurPatternTheme d) => i is EarsAccent or TailAccent ? d.BodyAccentColor.Average : d.BodyFillColor.Average;

	/// <inheritdoc cref="Rt"/>
	private static Color Ot(int i, GeneFurPattern?[] g) => (i is EarsAccent or TailAccent ? g[i]?.colorTwo : g[i]?.colorOne) ?? Color.black;

	private static readonly FurPatternTheme[] WhiteAndBlackPatternColors = [
		new(){
			skin = "7f7f7f00",
		},
		new(){
			skin = "ffffff",
		},
		new()
		{
			skin = "000000",
		}
	];

	public Window_FurPatternColorPicker(Pawn pawn)
	{
		this.pawn = pawn;
		geneFurPatterns[Skin] = new GeneFurPatternFill{colorOne = pawn.story.skinColorOverride ?? pawn.story.SkinColorBase, colorTwo = Color.white};
		geneFurPatterns[BodyFill] = pawn.GetGene<GeneFurPatternFill>();
		geneFurPatterns[BodyAccent] = pawn.GetGene<GeneFurPatternAccent>();
		geneFurPatterns[HeadFill] = pawn.GetGene<GeneHeadPatternFill>();
		geneFurPatterns[HeadAccent] = pawn.GetGene<GeneHeadPatternAccent>();
		geneFurPatterns[EarsAccent] = geneFurPatterns[EarsFill] = pawn.GetGene<GeneEarsWithPattern>();
		geneFurPatterns[TailAccent] = geneFurPatterns[TailFill] = pawn.GetGene<GeneTailWithPattern>();
		for (int i = Skin; i < ColorCount; ++i){
			selectedColors[i] = originalColors[i] = Ot(i, geneFurPatterns);
			selectedColorsAsDefVals[i] = selectedColors[i].ToDefVal();
			selectedColorsLuminosity[i] = selectedColors[i].CalculateBrightnessLevel();
			predefinedPatternColors[i] = WhiteAndBlackPatternColors.GroupBy(d => Ot(i, d)).Select(g => g.First()).ToArray();
		}
		selectedPattern = geneFurPatterns.Skip(1).FirstIndexOf(g => g != null);
		doCloseX = true;
		forcePause = true;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = true;
		closeOnAccept = false;
	}

	public override void Close(bool doCloseSound = true)
	{
		base.Close(doCloseSound);
		pawn.drawer.renderer.SetAllGraphicsDirty();
	}

	private void ResetColorValues(Color color)
	{
		selectedColorsLuminosity[selectedPattern] = color.CalculateBrightnessLevel();
		selectedColorsLuminosityAsString[selectedPattern] = selectedColorsLuminosity[selectedPattern].ToString();
		selectedColorsAsDefVals[selectedPattern] = color.WithBrightness((int)selectedColorsLuminosity[selectedPattern]).ToDefVal();
	}

	private static readonly Texture2D s_rotateButton = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");

	public override void DoWindowContents(Rect inRect)
	{
		using (TextBlock.Default())
		{
			var portrait = new Rect(inRect.x, inRect.y, 190, 240);
			Widgets.DrawMenuSection(portrait);
			Color[] oldColors = new Color[ColorCount];
			for (int i = Skin; i < ColorCount; ++i){
				oldColors[i] = Ot(i, geneFurPatterns);
				if (geneFurPatterns[i] != null)
					Rt(i, geneFurPatterns!) = selectedColors[i].WithBrightness((int)selectedColorsLuminosity[i]);
			}
			var originalSkinColorOverride = pawn.story.skinColorOverride;
			pawn.story.skinColorOverride = geneFurPatterns[Skin]!.colorOne;
			pawn.drawer.renderer.SetAllGraphicsDirty();
			var image = PortraitsCache.Get(pawn, new(200, 200), pawnFacingDirection, new(0, 0, 0.1f), healthStateOverride: PawnHealthState.Mobile, cameraZoom: 1.1f, renderClothes: false, renderHeadgear: false);
			for (int i = Skin; i < ColorCount; ++i){
				if (geneFurPatterns[i] != null)
					Rt(i, geneFurPatterns!) = oldColors[i];
			}
			pawn.story.skinColorOverride = originalSkinColorOverride;
			pawn.drawer.renderer.SetAllGraphicsDirty();
			GUI.DrawTexture(portrait, image, ScaleMode.ScaleAndCrop);
			var buttonRotate = new Rect(portrait.xMax - 24, portrait.y, 24, 24);
			if (Widgets.ButtonImage(buttonRotate, s_rotateButton))
				pawnFacingDirection = pawnFacingDirection.Rotated(RotationDirection.Clockwise);

			var layoutRect = new Rect(inRect.x + 200, inRect.y, inRect.width - 200, 240);
			RectDivider layout = new RectDivider(layoutRect, 195906069);

			#region Change fur pattern colors header text

			using (new TextBlock(GameFont.Medium))
			{
				var taggedString = "ColorPicker.ChangeFurPatternColors".Translate().CapitalizeFirst();
				var rectDivider6 = layout.NewRow(Text.CalcHeight(taggedString, layout.Rect.width));
				GUI.SetNextControlName(Dialog_ColorPickerBase.focusableControlNames[0]);
				Widgets.Label(rectDivider6, taggedString);
			}

			#endregion

			layout.NewRow(0f);
			var color = selectedColors[selectedPattern];
			var oldColor = color;

			#region ColorPalette

			float paletteHeight;
			using (new TextBlock(TextAnchor.MiddleLeft))
			{
				var rectDivider7 = layout;
				var rectDivider8 = rectDivider7.NewCol(250f, HorizontalJustification.Right);
				var colors = predefinedPatternColors[selectedPattern].Select(x => Ot(selectedPattern, x)).Distinct().ToList();
				colors.SortByColor(x => x);
				Widgets.ColorSelector(rectDivider8, ref color, colors, out paletteHeight
					// If we wanted to add tooltips for the predefined colors, we could do it here.
					//, extraOnGUI: (sColor, boxRect) => {
					//var firstDef = predefinedPatternColors[selectedPattern].FirstOrDefault(x => Ot(selectedPattern, x) == sColor);
					//if (firstDef != null)
					//    TooltipHandler.TipRegion(boxRect, "Tooltip here?");
					//}
				);
			}

			#endregion

			if (oldColor != color)
				ResetColorValues(color);

			#region Hue/Sat/Lum edit fields

			string defValue = selectedColorsAsDefVals[selectedPattern];
			var aggregator = new RectAggregator(new(layout.Rect.position, new(125f, 0f)), 195906069);
			bool hueOrSatChanged = Widgets.ColorTextfields(
				aggregator: ref aggregator,
				color: ref color,
				buffers: ref textFieldBuffers[selectedPattern],
				colorBuffer: ref textFieldColorBuffers[selectedPattern],
				previousFocusedControlName: previousFocusedControlName,
				controlName: nameof(hueAndSatFields),
				editable: hueAndSatFields,
				visible: hueAndSatFields
			);
			var size = aggregator.Rect.size;
			if (hueOrSatChanged)
			{
				Color.RGBToHSV(color, out var H, out var S, out _);
				float a = color.a;
				color = Color.HSVToRGB(H, S, 1f);
				color.a = a;
				defValue = selectedColorsAsDefVals[selectedPattern] = color.WithBrightness((int)selectedColorsLuminosity[selectedPattern]).ToDefVal();
			}
			var lumRectLabel = new Rect(layout.Rect.x, aggregator.Rect.yMax + 4, 50, 32);
			using (new TextBlock(TextAnchor.MiddleLeft))
				Widgets.Label(lumRectLabel, "ColorPicker.Lum".Translate());
			var lumRect = new Rect(lumRectLabel.xMax, lumRectLabel.y, 125 - 50, 32);
			var oldLum = selectedColorsLuminosity[selectedPattern];
			try
			{
				Widgets.TextFieldNumeric(lumRect, ref selectedColorsLuminosity[selectedPattern], ref selectedColorsLuminosityAsString[selectedPattern], min: 1f, max: 99f);
			}
			catch (Exception ex)
			{
				selectedColorsLuminosity[selectedPattern] = 1f;
				selectedColorsLuminosityAsString[selectedPattern] = selectedColorsLuminosity[selectedPattern].ToString();
			}

			if (!selectedColorsLuminosityAsString[selectedPattern].NullOrEmpty() && oldLum != selectedColorsLuminosity[selectedPattern])
				defValue = color.ToDefVal();

			if (Event.current.type == EventType.Layout)
				previousFocusedControlName = GUI.GetNameOfFocusedControl();

			var alternateColorPickerButtonRect = new Rect(lumRectLabel.x, lumRectLabel.yMax + 14, 160, 32);
			if (Widgets.ButtonText(alternateColorPickerButtonRect, "ColorPicker.AlternatePicker".Translate()))
				Find.WindowStack.Add(new Dialog_ColourPicker(color, ResetColorValues, new(windowRect.xMin + portrait.width + 17, windowRect.yMin + 14)));

			var defFieldLabel = new Rect(alternateColorPickerButtonRect.x, alternateColorPickerButtonRect.yMax + 4, 35, 32);
			using (new TextBlock(TextAnchor.MiddleLeft))
				Widgets.Label(defFieldLabel, "ColorPicker.ModCode".Translate());
			string oldDefValue = defValue;
			defValue = Widgets.TextField(new(defFieldLabel.xMax, defFieldLabel.y, 125, 32), defValue).Trim();
			if (ParseHelper.CanParse(typeof(Color), defValue))
			{
				color = ParseHelper.ParseColor(defValue);
				if (defValue != oldDefValue)
					ResetColorValues(color);
			}

			if (Event.current.type == EventType.Layout)
				previousFocusedControlName = GUI.GetNameOfFocusedControl();

			#endregion

			float height = Mathf.Max(paletteHeight, 128f, size.y);
			RectDivider rectDivider = layout.NewRow(height);
			rectDivider.NewCol(size.x);
			rectDivider.NewCol(250f, HorizontalJustification.Right);
			oldColor = color;
			Widgets.HSVColorWheel(rectDivider.Rect.ContractedBy((rectDivider.Rect.width - 128f) / 2f, (rectDivider.Rect.height - 128f) / 2f), ref color, ref hsvColorWheelDragging, 1f);
			// HSVColorWheel doesn't respect alpha, so we need to copy it back.
			color.a = oldColor.a;
			if (oldColor != color)
				ResetColorValues(color);

			layout = new(new(inRect.x, portrait.yMax + 24 + 15, inRect.width, inRect.height - portrait.height - (24 + 15)), 65436135);

			#region Cancel/Accept buttons

			var rectDivider5 = layout.NewRow(Dialog_ColorPickerBase.ButSize.y, VerticalJustification.Bottom);
			if (Widgets.ButtonText(rectDivider5.NewCol(Dialog_ColorPickerBase.ButSize.x), "Cancel".Translate()))
				Close();

			if (Widgets.ButtonText(rectDivider5.NewCol(Dialog_ColorPickerBase.ButSize.x, HorizontalJustification.Right), "Accept".Translate()))
			{
				for (int i = Skin; i < ColorCount; ++i){
					if (geneFurPatterns[i] != null && selectedColors[i] != originalColors[i])
						Rt(i, geneFurPatterns!) = selectedColors[i].WithBrightness((int)selectedColorsLuminosity[i]);
				}
				if (pawn.story.skinColorOverride == null
					? pawn.story.SkinColorBase != geneFurPatterns[Skin]!.colorOne
					: pawn.story.skinColorOverride != geneFurPatterns[Skin]!.colorOne
				)
				{
					pawn.story.skinColorOverride = geneFurPatterns[Skin]!.colorOne;
				}

				Close();
			}

			#endregion

			layout.NewRow(0f, VerticalJustification.Bottom);

			#region Current/Old color comparison rectangles

			Color color1 = selectedColors[selectedPattern].WithBrightness((int)selectedColorsLuminosity[selectedPattern]);
			Color oldColor1 = originalColors[selectedPattern];
			((Rect)layout).SplitVertically((((Rect)layout).width - 26f) / 2f, out var left, out var right);
			var rectDivider1 = new RectDivider(left, 195906069);
			var label = "CurrentColor".Translate().CapitalizeFirst();
			var label2 = "OldColor".Translate().CapitalizeFirst();
			float width = Mathf.Max(100f, label.GetWidthCached(), label2.GetWidthCached());
			var rectDivider2 = rectDivider1.NewRow(Text.LineHeight);
			Widgets.Label(rectDivider2.NewCol(width), label);
			Widgets.DrawBoxSolid(rectDivider2, color1);
			var rectDivider3 = rectDivider1.NewRow(Text.LineHeight);
			Widgets.Label(rectDivider3.NewCol(width), label2);
			Widgets.DrawBoxSolid(rectDivider3, oldColor1);
			var rectDivider4 = new RectDivider(right, 195906069);
			rectDivider4.NewCol(26f);

			#endregion

			selectedColors[selectedPattern] = color;
			var buttonsRect = new Rect(inRect.x, portrait.yMax + 10, 90, 24);
			Widgets.Label(buttonsRect, $"ColorPicker.{selectedPattern switch {
				BodyFill => nameof(BodyFill),
				BodyAccent => nameof(BodyAccent),
				HeadFill => nameof(HeadFill),
				HeadAccent => nameof(HeadAccent),
				EarsFill => nameof(EarsFill),
				EarsAccent => nameof(EarsAccent),
				TailFill => nameof(TailFill),
				TailAccent => nameof(TailAccent),
				_/*Skin*/ => nameof(Skin)
			}}".Translate());
			buttonsRect = new(buttonsRect.xMax, buttonsRect.y, 50, buttonsRect.width);

			for (int i = Skin; i < ColorCount; ++i){
				if (Widgets.RadioButton(new(buttonsRect.xMax + 36 * i, buttonsRect.y), selectedPattern == i))
					selectedPattern = i;
			}

			if (Event.current.type == EventType.KeyDown)
			{
				if (Event.current.keyCode is KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.W or KeyCode.S or KeyCode.Tab)
				{
					Event.current.Use();
					string controlName = GUI.GetNameOfFocusedControl();
					int controlIndex = controlName.NullOrEmpty()
						? Dialog_ColorPickerBase.focusableControlNames.Count - 1
						: Dialog_ColorPickerBase.focusableControlNames.IndexOf(controlName)
					;

					if (Event.current.keyCode == KeyCode.Tab)
					{
						controlIndex += Event.current.shift ? -1 : 1;
						if (controlIndex < 0)
							controlIndex = Dialog_ColorPickerBase.focusableControlNames.Count - 1;
						else if (controlIndex >= Dialog_ColorPickerBase.focusableControlNames.Count)
							controlIndex = 0;

						GUI.FocusControl(Dialog_ColorPickerBase.focusableControlNames[controlIndex]);
					}
					else
					{
						selectedColorsLuminosityAsString[selectedPattern] = (selectedColorsLuminosity[selectedPattern] += Event.current.keyCode is KeyCode.UpArrow or KeyCode.W ? 1f : -1f).ToString();
					}
				}
			}

			if (Event.current.type == EventType.Layout)
				previousFocusedControlName = GUI.GetNameOfFocusedControl();
		}
	}
}