namespace Cheechin;

[StaticConstructorOnStartup]
public sealed class Window_FurPatternColorPicker: Window
{
    private readonly Color patternColorFillOriginal;

    private readonly Color patternColorAccentOriginal;

    private Color patternColorFill;

    private Color patternColorAccent;

    private string patternColorFillHex;

    private string patternColorAccentHex;

    private float patternColorFillLuminosity;

    private float patternColorAccentLuminosity;

    private string luminosityBuf1;

    private string luminosityBuf2;

    private bool accentColorChosen;

    private readonly GeneFurPatternFill? geneFurPatternFill;

    private readonly GeneFurPatternAccent? geneFurPatternAccent;

    private bool hsvColorWheelDragging;

    private string[] textfieldBuffersOne = new string[6];

    private string[] textfieldBuffersTwo = new string[6];

    private Color textfieldColorBufferOne, textfieldColorBufferTwo;

    private string previousFocusedControlName;

    public static Widgets.ColorComponents visibleColorTextFields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;

    public static Widgets.ColorComponents editableColorTextFields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;

    public override Vector2 InitialSize => new(800f, 410f);

    private readonly FurPatternColorDef[] predefinedFurPatternFillColors;

    private readonly FurPatternColorDef[] predefinedFurPatternAccentColors;

    private Rot4 pawnRot = Rot4.South;

    public Window_FurPatternColorPicker(Pawn pawn)
    {
        geneFurPatternFill = pawn.GetGeneFurPatternFill();
        geneFurPatternAccent = pawn.GetGeneFurPatternAccent();
        doCloseX = true;
        patternColorFill = patternColorFillOriginal = geneFurPatternFill?.colorOne ?? Color.black;
        patternColorAccent = patternColorAccentOriginal = geneFurPatternAccent?.colorOne ?? Color.black;
        patternColorFillHex = "#" + ColorUtility.ToHtmlStringRGB(patternColorFill);
        patternColorAccentHex = "#" + ColorUtility.ToHtmlStringRGB(patternColorAccent);
        patternColorFillLuminosity = patternColorFill.CalculateBrightnessLevel();
        patternColorAccentLuminosity = patternColorAccent.CalculateBrightnessLevel();
        forcePause = true;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        predefinedFurPatternFillColors = geneFurPatternFill?.def.GetModExtension<FurPatternColors>().predefinedFurPatternColors.OrderBy(x => x.displayOrder).ToArray() ?? [];
        predefinedFurPatternAccentColors = geneFurPatternAccent?.def.GetModExtension<FurPatternColors>().predefinedFurPatternColors.OrderBy(x => x.displayOrder).ToArray() ?? [];
    }

    private static void HeaderRow(ref RectDivider layout)
    {
        using (new TextBlock(GameFont.Medium))
        {
            var taggedString = "ColorPicker.ChangeFurPatternColors".Translate().CapitalizeFirst();
            var rectDivider = layout.NewRow(Text.CalcHeight(taggedString, layout.Rect.width));
            GUI.SetNextControlName(Dialog_ColorPickerBase.focusableControlNames[0]);
            Widgets.Label(rectDivider, taggedString);
        }
    }

    public override void Close(bool doCloseSound = true)
    {
        base.Close(doCloseSound);
        geneFurPatternFill?.ApplyColors();
        geneFurPatternAccent?.ApplyColors();
    }

    private void BottomButtons(ref RectDivider layout)
    {
        var rectDivider = layout.NewRow(Dialog_ColorPickerBase.ButSize.y, VerticalJustification.Bottom);
        if (Widgets.ButtonText(rectDivider.NewCol(Dialog_ColorPickerBase.ButSize.x), "Cancel".Translate()))
            Close();

        if (Widgets.ButtonText(rectDivider.NewCol(Dialog_ColorPickerBase.ButSize.x, HorizontalJustification.Right), "Accept".Translate()))
        {
            if (geneFurPatternFill != null && patternColorFill != patternColorFillOriginal)
                geneFurPatternFill.colorOne = patternColorFill.WithBrightness((int)patternColorFillLuminosity);

            if (geneFurPatternAccent != null && patternColorAccent != patternColorAccentOriginal)
                geneFurPatternAccent.colorOne = patternColorAccent.WithBrightness((int)patternColorAccentLuminosity);

            Close();
        }
    }

    private void ResetColorValues(Color color)
    {
        if (accentColorChosen)
        {
            patternColorAccentLuminosity = color.CalculateBrightnessLevel();
            luminosityBuf2 = patternColorAccentLuminosity.ToString();
            patternColorAccentHex = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorAccentLuminosity));
        }
        else
        {
            patternColorFillLuminosity = color.CalculateBrightnessLevel();
            luminosityBuf1 = patternColorFillLuminosity.ToString();
            patternColorFillHex = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorFillLuminosity));
        }
    }

    private string ResetHexValues(Color color)
    {
        if (accentColorChosen)
            patternColorAccentHex = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorAccentLuminosity));
        else
            patternColorFillHex = "#" + ColorUtility.ToHtmlStringRGB(color.WithBrightness((int)patternColorFillLuminosity));

        return patternColorFillHex;
    }

    private void ColorFields(ref RectDivider layout, ref Color color, string hexValue, ref float lumValue, ref string lumBuf, ref string[] textfieldBuffers, ref Color textfieldColorBuffer, out Vector2 size)
    {
        var aggregator = new RectAggregator(new(layout.Rect.position, new(125f, 0f)), 195906069);
        bool num = Widgets.ColorTextfields(ref aggregator, ref color, ref textfieldBuffers, ref textfieldColorBuffer, previousFocusedControlName, "colorTextfields", editableColorTextFields, visibleColorTextFields);
        size = aggregator.Rect.size;
        if (num)
        {
            Color.RGBToHSV(color, out var H, out var S, out _);
            color = Color.HSVToRGB(H, S, 1f);
            hexValue = ResetHexValues(color);
        }
        var lumRectLabel = new Rect(layout.Rect.x, aggregator.Rect.yMax + 4, 50, 32);
        using (new TextBlock(TextAnchor.MiddleLeft))
            Widgets.Label(lumRectLabel, "ColorPicker.Lum".Translate());
        var lumRect = new Rect(lumRectLabel.xMax, lumRectLabel.y, 125 - 50, 32);
        var oldLum = lumValue;
        try
        {
            Widgets.TextFieldNumeric(lumRect, ref lumValue, ref lumBuf, min: 1f, max: 99f);
        }
        catch (Exception ex)
        {
            lumValue = 1f;
            lumBuf = lumValue.ToString();
        }
        if (!lumBuf.NullOrEmpty() && oldLum != lumValue)
            hexValue = ResetHexValues(color);

        if (Event.current.type == EventType.Layout)
            previousFocusedControlName = GUI.GetNameOfFocusedControl();

        var hexRectLabel = new Rect(lumRectLabel.x, lumRectLabel.yMax + 4, 50, 32);
        using (new TextBlock(TextAnchor.MiddleLeft))
            Widgets.Label(hexRectLabel, "ColorPicker.HexCode".Translate());
        var hexRect = new Rect(hexRectLabel.xMax, hexRectLabel.y, 125 - 50, 32);
        var oldValue = hexValue;
        hexValue = Widgets.TextField(hexRect, hexValue).Trim();
        if (Utility.TryGetColorFromHex(hexValue, out var tempColor))
        {
            color = tempColor;
            if (hexValue != oldValue)
                ResetColorValues(color);
        }
        if (Event.current.type == EventType.Layout)
            previousFocusedControlName = GUI.GetNameOfFocusedControl();
    }

    private static void ColorReadback(Rect rect, Color color, Color oldColor)
    {
        rect.SplitVertically((rect.width - 26f) / 2f, out var left, out var right);
        var rectDivider = new RectDivider(left, 195906069);
        var label = "CurrentColor".Translate().CapitalizeFirst();
        var label2 = "OldColor".Translate().CapitalizeFirst();
        float width = Mathf.Max(100f, label.GetWidthCached(), label2.GetWidthCached());
        var rectDivider2 = rectDivider.NewRow(Text.LineHeight);
        Widgets.Label(rectDivider2.NewCol(width), label);
        Widgets.DrawBoxSolid(rectDivider2, color);
        var rectDivider3 = rectDivider.NewRow(Text.LineHeight);
        Widgets.Label(rectDivider3.NewCol(width), label2);
        Widgets.DrawBoxSolid(rectDivider3, oldColor);
        var rectDivider4 = new RectDivider(right, 195906069);
        rectDivider4.NewCol(26f);
    }

    private static readonly Texture2D s_rotateButton = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");

    public override void DoWindowContents(Rect inRect)
    {
        using (TextBlock.Default())
        {
            var portrait = new Rect(inRect.x, inRect.y, 190, 240);
            Widgets.DrawMenuSection(portrait);
            var oldFillColor = geneFurPatternFill?.colorOne ?? Color.black;
            var oldAccentColor = geneFurPatternAccent?.colorOne ?? Color.black;
            if (geneFurPatternFill != null)
                geneFurPatternFill.colorOne = patternColorFill.WithBrightness((int)patternColorFillLuminosity);
            if (geneFurPatternAccent != null)
                geneFurPatternAccent.colorOne = patternColorAccent.WithBrightness((int)patternColorAccentLuminosity);
            geneFurPatternFill?.ApplyColors();
            geneFurPatternAccent?.ApplyColors();
            var image = PortraitsCache.Get(geneFurPatternAccent?.pawn ?? geneFurPatternFill?.pawn, new(200, 200), pawnRot, new(0, 0, 0.1f), healthStateOverride: PawnHealthState.Mobile, cameraZoom: 1.1f, renderClothes: false, renderHeadgear: false);
            if (geneFurPatternFill != null)
                geneFurPatternFill.colorOne = oldFillColor;
            if (geneFurPatternAccent != null)
                geneFurPatternAccent.colorOne = oldAccentColor;
            geneFurPatternFill?.ApplyColors();
            geneFurPatternAccent?.ApplyColors();
            GUI.DrawTexture(portrait, image, ScaleMode.ScaleAndCrop);
            var buttonRotate = new Rect(portrait.xMax - 24, portrait.y, 24, 24);
            if (Widgets.ButtonImage(buttonRotate, s_rotateButton))
                pawnRot = pawnRot.Rotated(RotationDirection.Clockwise);

            var layoutRect = new Rect(inRect.x + 200, inRect.y, inRect.width - 200, 240);
            RectDivider layout = new RectDivider(layoutRect, 195906069);
            HeaderRow(ref layout);
            layout.NewRow(0f);
            var color = accentColorChosen ? patternColorAccent : patternColorFill;
            var oldColor = color;
            ColorPalette(ref layout, ref color, out var paletteHeight);
            if (oldColor != color)
                ResetColorValues(color);

            Vector2 size;
            if (accentColorChosen)
                ColorFields(ref layout, ref color, patternColorAccentHex, ref patternColorAccentLuminosity, ref luminosityBuf2, ref textfieldBuffersTwo, ref textfieldColorBufferTwo, out size);
            else
                ColorFields(ref layout, ref color, patternColorFillHex, ref patternColorFillLuminosity, ref luminosityBuf1, ref textfieldBuffersOne, ref textfieldColorBufferOne, out size);

            float height = Mathf.Max(paletteHeight, 128f, size.y);
            RectDivider rectDivider = layout.NewRow(height);
            rectDivider.NewCol(size.x);
            rectDivider.NewCol(250f, HorizontalJustification.Right);
            oldColor = color;
            Widgets.HSVColorWheel(rectDivider.Rect.ContractedBy((rectDivider.Rect.width - 128f) / 2f,
                (rectDivider.Rect.height - 128f) / 2f), ref color, ref hsvColorWheelDragging, 1f);
            if (oldColor != color)
                ResetColorValues(color);

            layout = new(new(inRect.x, portrait.yMax + 24 + 15, inRect.width,
                inRect.height - portrait.height - (24 + 15)), 65436135);
            BottomButtons(ref layout);
            layout.NewRow(0f, VerticalJustification.Bottom);
            if (accentColorChosen)
            {
                ColorReadback(layout, patternColorAccent.WithBrightness((int)patternColorAccentLuminosity), patternColorAccentOriginal);
                patternColorAccent = color;
            }
            else
            {
                ColorReadback(layout, patternColorFill.WithBrightness((int)patternColorFillLuminosity), patternColorFillOriginal);
                patternColorFill = color;
            }

            var buttonsRect = new Rect(inRect.x, portrait.yMax + 10, 117, 24);
            Widgets.Label(buttonsRect, "ColorPicker.FurPattern".Translate());
            buttonsRect = new(buttonsRect.xMax, buttonsRect.y, 50, buttonsRect.width);
            Widgets.Label(buttonsRect, (accentColorChosen ? "ColorPicker.ColorAccent" : "ColorPicker.ColorFill").Translate());

            if (Widgets.RadioButton(new(buttonsRect.xMax, buttonsRect.y), !accentColorChosen))
                accentColorChosen = false;

            if (Widgets.RadioButton(new(buttonsRect.xMax + 40, buttonsRect.y), accentColorChosen))
                accentColorChosen = true;

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                bool num = !Event.current.shift;
                Event.current.Use();
                string text = GUI.GetNameOfFocusedControl();
                if (text.NullOrEmpty())
                    text = Dialog_ColorPickerBase.focusableControlNames[0];

                int num2 = Dialog_ColorPickerBase.focusableControlNames.IndexOf(text);
                if (num2 < 0)
                    num2 = Dialog_ColorPickerBase.focusableControlNames.Count;

                num2 += num ? 1 : -1;
                if (num2 >= Dialog_ColorPickerBase.focusableControlNames.Count)
                    num2 = 0;

                else if (num2 < 0)
                    num2 = Dialog_ColorPickerBase.focusableControlNames.Count - 1;

                GUI.FocusControl(Dialog_ColorPickerBase.focusableControlNames[num2]);
            }

            if (Event.current.type == EventType.Layout)
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
        }
    }

    private void ColorSelectorExtraOnGui(Color color, Rect boxRect)
    {
        var firstDef = (accentColorChosen ? predefinedFurPatternFillColors : predefinedFurPatternAccentColors).FirstOrDefault(x => !x.blacklistPrimary && x.colorOne == color);

        if (firstDef != null)
            TooltipHandler.TipRegion(boxRect, firstDef.LabelCap);
    }

    private void ColorPalette(ref RectDivider layout, ref Color color, out float paletteHeight)
    {
        using (new TextBlock(TextAnchor.MiddleLeft))
        {
            var rectDivider = layout;
            var rectDivider2 = rectDivider.NewCol(250f, HorizontalJustification.Right);
            var colors = (accentColorChosen ? predefinedFurPatternAccentColors : predefinedFurPatternFillColors).Where(x => !x.blacklistPrimary).Select(x => x.colorOne).Distinct().ToList();
            colors.SortByColor(x => x);
            Widgets.ColorSelector(rectDivider2, ref color, colors, out paletteHeight, extraOnGUI: ColorSelectorExtraOnGui);
        }
    }
}