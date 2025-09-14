namespace Cheechin;

[StaticConstructorOnStartup]
public sealed class Window_FurPatternColorPicker: Window
{
    private const int BodyFill = 0;
    private const int BodyAccent = 1;
    private const int HeadFill = 2;
    private const int HeadAccent = 3;
    private const int PatternCount = 4;

    private readonly Color[] patternColorsOriginal = new Color[PatternCount];

    private readonly Color[] patternColors = new Color[PatternCount];

    private readonly string[] patternColorsHex = new string[PatternCount];

    private readonly float[] patternColorsLuminosity = new float[PatternCount];

    private string[] luminosityBuf = new string[PatternCount];

    private int selectedPattern;

    private readonly GeneFurPattern?[] geneFurPatterns = new GeneFurPattern[PatternCount];

    private bool hsvColorWheelDragging;

    private readonly string[][] textfieldBuffers = [new string[6],new string[6],new string[6],new string[6]];

    private readonly Color[] textfieldColorBuffers = new Color[PatternCount];

    private string previousFocusedControlName;

    public const Widgets.ColorComponents colorTextFields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;

    public override Vector2 InitialSize => new(800f, 410f);

    private readonly FurPatternColorDef[][] predefinedPatternColors = new FurPatternColorDef[PatternCount][];

    private Rot4 pawnRot = Rot4.South;

    private readonly Pawn pawn;

    public Window_FurPatternColorPicker(Pawn pawn)
    {
        this.pawn = pawn;
        geneFurPatterns[BodyFill] = pawn.GetGene<GeneFurPatternFill>();
        geneFurPatterns[BodyAccent] = pawn.GetGene<GeneFurPatternAccent>();
        geneFurPatterns[HeadFill] = pawn.GetGene<GeneHeadPatternFill>();
        geneFurPatterns[HeadAccent] = pawn.GetGene<GeneHeadPatternAccent>();
        for (int i = BodyFill; i < PatternCount; ++i){
            patternColors[i] = patternColorsOriginal[i] = geneFurPatterns[i]?.colorOne ?? Color.black;
            patternColorsHex[i] = "#" + ColorUtility.ToHtmlStringRGB(patternColors[i]);
            patternColorsLuminosity[i] = patternColors[i].CalculateBrightnessLevel();
            predefinedPatternColors[i] = geneFurPatterns[i]?.def.GetModExtension<FurPatternColors>().predefinedFurPatternColors.OrderBy(x => x.displayOrder).ToArray() ?? [];
        }
        selectedPattern = geneFurPatterns.FirstIndexOf(g => g != null);
        doCloseX = true;
        forcePause = true;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        closeOnAccept = false;
    }

    public override void Close(bool doCloseSound = true)
    {
        base.Close(doCloseSound);
        pawn.drawer.renderer.SetAllGraphicsDirty();
    }

    private void ResetColorValues(Color color)
    {
        patternColorsLuminosity[selectedPattern] = color.CalculateBrightnessLevel();
        luminosityBuf[selectedPattern] = patternColorsLuminosity[selectedPattern].ToString();
        patternColorsHex[selectedPattern] = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorsLuminosity[selectedPattern]));
    }

    private string ResetHexValues(Color color) => patternColorsHex[selectedPattern] = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorsLuminosity[selectedPattern]));

    private static readonly Texture2D s_rotateButton = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");

    public override void DoWindowContents(Rect inRect)
    {
        using (TextBlock.Default())
        {
            var portrait = new Rect(inRect.x, inRect.y, 190, 240);
            Widgets.DrawMenuSection(portrait);
            var oldColors = geneFurPatterns.Select(g => g?.colorOne ?? Color.black).ToArray();
            for (int i = BodyFill; i < PatternCount; ++i){
                if (geneFurPatterns[i] != null)
                    geneFurPatterns[i]!.colorOne = patternColors[i].WithBrightness((int)patternColorsLuminosity[i]);
            }
            pawn.drawer.renderer.SetAllGraphicsDirty();
            var image = PortraitsCache.Get(pawn, new(200, 200), pawnRot, new(0, 0, 0.1f), healthStateOverride: PawnHealthState.Mobile, cameraZoom: 1.1f, renderClothes: false, renderHeadgear: false);
            for (int i = BodyFill; i < PatternCount; ++i){
                if (geneFurPatterns[i] != null)
                    geneFurPatterns[i]!.colorOne = oldColors[i];
            }
            pawn.drawer.renderer.SetAllGraphicsDirty();
            GUI.DrawTexture(portrait, image, ScaleMode.ScaleAndCrop);
            var buttonRotate = new Rect(portrait.xMax - 24, portrait.y, 24, 24);
            if (Widgets.ButtonImage(buttonRotate, s_rotateButton))
                pawnRot = pawnRot.Rotated(RotationDirection.Clockwise);

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
            var color = patternColors[selectedPattern];
            var oldColor = color;
            ColorPalette(ref layout, ref color, out var paletteHeight);
            if (oldColor != color)
                ResetColorValues(color);

            #region Hue/Sat/Lum edit fields

            string hexValue = patternColorsHex[selectedPattern];
            var aggregator = new RectAggregator(new(layout.Rect.position, new(125f, 0f)), 195906069);
            bool hueOrSatChanged = Widgets.ColorTextfields(
                aggregator: ref aggregator,
                color: ref color,
                buffers: ref textfieldBuffers[selectedPattern],
                colorBuffer: ref textfieldColorBuffers[selectedPattern],
                previousFocusedControlName: previousFocusedControlName,
                controlName: nameof(colorTextFields),
                editable: colorTextFields,
                visible: colorTextFields
            );
            var size = aggregator.Rect.size;
            if (hueOrSatChanged)
            {
                Color.RGBToHSV(color, out var H, out var S, out _);
                color = Color.HSVToRGB(H, S, 1f);
                hexValue = ResetHexValues(color);
            }
            var lumRectLabel = new Rect(layout.Rect.x, aggregator.Rect.yMax + 4, 50, 32);
            using (new TextBlock(TextAnchor.MiddleLeft))
                Widgets.Label(lumRectLabel, "ColorPicker.Lum".Translate());
            var lumRect = new Rect(lumRectLabel.xMax, lumRectLabel.y, 125 - 50, 32);
            var oldLum = patternColorsLuminosity[selectedPattern];
            try
            {
                Widgets.TextFieldNumeric(lumRect, ref patternColorsLuminosity[selectedPattern], ref luminosityBuf[selectedPattern], min: 1f, max: 99f);
            }
            catch (Exception ex)
            {
                patternColorsLuminosity[selectedPattern] = 1f;
                luminosityBuf[selectedPattern] = patternColorsLuminosity[selectedPattern].ToString();
            }
            if (!luminosityBuf[selectedPattern].NullOrEmpty() && oldLum != patternColorsLuminosity[selectedPattern])
                hexValue = ResetHexValues(color);

            if (Event.current.type == EventType.Layout)
                previousFocusedControlName = GUI.GetNameOfFocusedControl();

            var hexRectLabel = new Rect(lumRectLabel.x, lumRectLabel.yMax + 4, 50, 32);
            using (new TextBlock(TextAnchor.MiddleLeft))
                Widgets.Label(hexRectLabel, "ColorPicker.HexCode".Translate());
            var hexRect = new Rect(hexRectLabel.xMax, hexRectLabel.y, 125 - 50, 32);
            string oldValue = hexValue;
            hexValue = Widgets.TextField(hexRect, hexValue).Trim();
            if (Utility.TryGetColorFromHex(hexValue, out var tempColor))
            {
                color = tempColor;
                if (hexValue != oldValue)
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
            if (oldColor != color)
                ResetColorValues(color);

            layout = new(new(inRect.x, portrait.yMax + 24 + 15, inRect.width, inRect.height - portrait.height - (24 + 15)), 65436135);

            #region Cancel/Accept buttons

            var rectDivider5 = layout.NewRow(Dialog_ColorPickerBase.ButSize.y, VerticalJustification.Bottom);
            if (Widgets.ButtonText(rectDivider5.NewCol(Dialog_ColorPickerBase.ButSize.x), "Cancel".Translate()))
                Close();

            if (Widgets.ButtonText(rectDivider5.NewCol(Dialog_ColorPickerBase.ButSize.x, HorizontalJustification.Right), "Accept".Translate()))
            {
                for (int i1 = BodyFill; i1 < PatternCount; ++i1){
                    if (geneFurPatterns[i1] != null && patternColors[i1] != patternColorsOriginal[i1])
                        geneFurPatterns[i1]!.colorOne = patternColors[i1].WithBrightness((int)patternColorsLuminosity[i1]);
                }
                Close();
            }

            #endregion

            layout.NewRow(0f, VerticalJustification.Bottom);

            #region Current/Old color comparison rectangles

            Color color1 = patternColors[selectedPattern].WithBrightness((int)patternColorsLuminosity[selectedPattern]);
            Color oldColor1 = patternColorsOriginal[selectedPattern];
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

            patternColors[selectedPattern] = color;
            var buttonsRect = new Rect(inRect.x, portrait.yMax + 10, 117, 24);
            Widgets.Label(buttonsRect, $"ColorPicker.{selectedPattern switch {BodyFill => nameof(BodyFill), BodyAccent => nameof(BodyAccent), HeadFill => nameof(HeadFill), /*HeadAccent*/_ => nameof(HeadAccent)}}".Translate());
            buttonsRect = new(buttonsRect.xMax, buttonsRect.y, 50, buttonsRect.width);

            if (Widgets.RadioButton(new(buttonsRect.xMax, buttonsRect.y), selectedPattern == BodyFill))
                selectedPattern = BodyFill;

            if (Widgets.RadioButton(new(buttonsRect.xMax + 40, buttonsRect.y), selectedPattern == BodyAccent))
                selectedPattern = BodyAccent;

            if (Widgets.RadioButton(new(buttonsRect.xMax + 80, buttonsRect.y), selectedPattern == HeadFill))
                selectedPattern = HeadFill;

            if (Widgets.RadioButton(new(buttonsRect.xMax + 120, buttonsRect.y), selectedPattern == HeadAccent))
                selectedPattern = HeadAccent;

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
                        luminosityBuf[selectedPattern] = (patternColorsLuminosity[selectedPattern] += Event.current.keyCode is KeyCode.UpArrow or KeyCode.W ? 1f : -1f).ToString();
                    }
                }
            }

            if (Event.current.type == EventType.Layout)
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
        }
    }

    private void ColorSelectorExtraOnGui(Color color, Rect boxRect)
    {
        var firstDef = predefinedPatternColors[selectedPattern].FirstOrDefault(x => !x.blacklistPrimary && x.colorOne == color);

        if (firstDef != null)
            TooltipHandler.TipRegion(boxRect, firstDef.LabelCap);
    }

    private void ColorPalette(ref RectDivider layout, ref Color color, out float paletteHeight)
    {
        using (new TextBlock(TextAnchor.MiddleLeft))
        {
            var rectDivider = layout;
            var rectDivider2 = rectDivider.NewCol(250f, HorizontalJustification.Right);
            var colors = predefinedPatternColors[selectedPattern].Where(x => !x.blacklistPrimary).Select(x => x.colorOne).Distinct().ToList();
            colors.SortByColor(x => x);
            Widgets.ColorSelector(rectDivider2, ref color, colors, out paletteHeight, extraOnGUI: ColorSelectorExtraOnGui);
        }
    }
}