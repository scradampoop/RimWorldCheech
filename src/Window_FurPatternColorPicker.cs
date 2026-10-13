using System.IO;

namespace Cheechin;

/// <summary>
/// Persists the user's recent ad-hoc color choices to disk so they show up as swatches across sessions.
/// Imported wholesale from the Fluffy ColourPicker codebase at https://github.com/fluffy-mods/ColourPicker.
/// </summary>
public sealed class RecentColours
{
	private const int Max = 20;
	private static List<Color> _colors = [];

	static RecentColours()
	{
		string path = Path.Combine(GenFilePaths.ConfigFolderPath, "ColourPicker.xml");
		if (!File.Exists(path)) {
			return;
		}

		try {
			Scribe.loader.InitLoading(path);
			ExposeData();
		} catch (Exception ex) {
			Log.Error("ColourPicker :: Error loading recent colours from file:" + ex);
		} finally {
			Scribe.loader.FinalizeLoading();
		}
	}

	public Color this[int index] => _colors[index];

	public int Count => _colors.Count;

	public void Add(Color color) {
		_colors.RemoveAll(c => c == color);
		_colors.Insert(0, color);

		while (_colors.Count > Max) {
			_colors.RemoveAt(_colors.Count - 1);
		}

		try {
			string path = Path.Combine(GenFilePaths.ConfigFolderPath, "ColourPicker.xml");
			Scribe.saver.InitSaving(path, "ColourPicker");
			ExposeData();
		} catch (Exception ex) {
			Log.Error("ColourPicker :: Error saving recent colours to file:" + ex);
		} finally {
			Scribe.saver.FinalizeSaving();
		}
	}

	private static void ExposeData() => Scribe_Collections.Look(ref _colors, "RecentColors");
}

/// <summary>
/// Small text input bound to a typed value with optional validation. Imported wholesale from the Fluffy ColourPicker codebase.
/// </summary>
public sealed class TextField<T>(T value, string id, Action<T> callback, Func<string, T>? _parser = null, Func<string, bool>? _validator = null, Func<T, string>? _toString = null)
{
	private T _value = value;
	private string _temp = value.ToString();

	public T Value {
		get => _value;
		set {
			_value = value;
			_temp = _toString?.Invoke(value) ?? value.ToString();
		}
	}

	public static TextField<float> Float01(float value, string id, Action<float> callback) => new(value, id, callback, float.Parse, Validate01, f => Round(f).ToString());

	public static TextField<string> Hex(string value, string id, Action<string> callback) => new(value, id, callback, hex => hex, ValidateHex);

	public void Draw(Rect rect) {
		bool valid = _validator?.Invoke(_temp) ?? true;
		GUI.color = valid ? Color.white : Color.red;
		GUI.SetNextControlName(id);
		string temp = Widgets.TextField(rect, _temp);
		GUI.color = Color.white;

		if (temp != _temp) {
			_temp = temp;
			if (_validator?.Invoke(_temp) ?? true) {
				_value = _parser(_temp);
				callback?.Invoke(_value);
			}
		}
	}

	private static bool Validate01(string value) {
		if (!float.TryParse(value, out float parsed)) {
			return false;
		}

		return parsed is >= 0f and <= 1f;
	}

	private static bool ValidateHex(string value) {
		return ColorUtility.TryParseHtmlString(value, out _);
	}

	private static float Round(float value, int digits = 2) {
		float exponent = Mathf.Pow(10, digits);
		return Mathf.RoundToInt(value * exponent) / exponent;
	}
}

/// <summary>
/// Per-pawn fur color picker. The HSV/RGB/hex picker UI is ported from Fluffy's ColourPicker (https://github.com/fluffy-mods/ColourPicker as of 2025-09-14).
/// The portrait preview and the coat pattern dropdown are specific to this mod.
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

	private readonly Color[] _originalColors = new Color[ColorCount];
	private readonly Color[] _selectedColors = new Color[ColorCount];
	private readonly GeneFurPattern?[] _geneFurPatterns = new GeneFurPattern[ColorCount];
	private int _selectedPattern;

	private Rot4 _pawnFacingDirection = Rot4.South;
	private readonly Pawn pawn;
	private static readonly Texture2D s_rotateButton = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");

	private Controls _activeControl = Controls.None;

	private readonly Color
		_alphaBGColorA = Color.white,
		_alphaBGColorB = new(.85f, .85f, .85f);

	private Texture2D?
		_colourPickerBG,
		_huePickerBG,
		_alphaPickerBG,
		_tempPreviewBG,
		_previewBG;

	private string? _hex;
	private const float _margin = 6f;
	private const float _buttonHeight = 30f;
	private const float _fieldHeight = 24f;
	private float _huePosition;
	private float _alphaPosition;
	private float _h;
	private float _s;
	private float _v;
	private const int
		_pickerSize = 300,
		_sliderWidth = 15,
		_alphaBGBlockSize = 10,
		_previewSize = 90,
		_handleSize = 10,
		_recentSize = 20;

	private Vector2 _position = Vector2.zero;

	private readonly RecentColours _recentColours = new();

	public Color curColour;
	public Color tempColour;

	private readonly TextField<string> HexField;
	private readonly TextField<float>
		RedField,
		GreenField,
		BlueField,
		HueField,
		SaturationField,
		ValueField,
		Alpha1Field,
		Alpha2Field;

	private readonly List<string> _textFieldIds;

	private const float PortraitWidth = 380f;
	private const float PortraitHeight = 340f;
	private const float ColumnGap = 12f;
	private const float PatternDropdownHeight = 30f;

	public override Vector2 InitialSize => new(
		PortraitWidth + ColumnGap + _pickerSize + (3 * _margin) + (2 * _sliderWidth) + (2 * _previewSize) + (Margin * 2f),
		Mathf.Max(PortraitHeight, _pickerSize + _margin + PatternDropdownHeight) + (Margin * 2f)
	);

	/// <summary>
	/// EarsAccent and TailAccent map onto each gene's <c>colorTwo</c>; everything else uses <c>colorOne</c>. This color picker class started to get ugly when we use colorTwo in some cases and not others. scradam swears he doesn't normally write code this weird. He just didn't want to refactor it all just yet.
	/// </summary>
	private static ref Color Rt(int i, GeneFurPattern[] g) => ref (i is EarsAccent or TailAccent ? ref g[i].colorTwo : ref g[i].colorOne);

	/// <inheritdoc cref="Rt"/>
	private static Color Ot(int i, GeneFurPattern?[] g) => (i is EarsAccent or TailAccent ? g[i]?.colorTwo : g[i]?.colorOne) ?? Color.black;

	public Window_FurPatternColorPicker(Pawn pawn)
	{
		this.pawn = pawn;
		_geneFurPatterns[Skin] = new GeneFurPatternFill{colorOne = pawn.story.skinColorOverride ?? pawn.story.SkinColorBase, colorTwo = Color.white};
		_geneFurPatterns[BodyFill] = pawn.GetGene<GeneFurPatternFill>();
		_geneFurPatterns[BodyAccent] = pawn.GetGene<GeneFurPatternAccent>();
		_geneFurPatterns[HeadFill] = pawn.GetGene<GeneHeadPatternFill>();
		_geneFurPatterns[HeadAccent] = pawn.GetGene<GeneHeadPatternAccent>();
		_geneFurPatterns[EarsAccent] = _geneFurPatterns[EarsFill] = pawn.GetGene<GeneEarsWithPattern>();
		_geneFurPatterns[TailAccent] = _geneFurPatterns[TailFill] = pawn.GetGene<GeneTailWithPattern>();
		for (int i = Skin; i < ColorCount; ++i)
			_selectedColors[i] = _originalColors[i] = Ot(i, _geneFurPatterns);

		int firstActive = -1;
		for (int i = BodyFill; i < ColorCount; ++i)
		{
			if (_geneFurPatterns[i] != null)
			{
				firstActive = i;
				break;
			}
		}
		_selectedPattern = firstActive < 0 ? Skin : firstActive;

		curColour = _originalColors[_selectedPattern];
		tempColour = _selectedColors[_selectedPattern];

		doCloseX = true;
		forcePause = true;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = true;
		closeOnAccept = false;

		HueField = TextField<float>.Float01(_h, "Hue", h => H = h);
		SaturationField = TextField<float>.Float01(_s, "Saturation", s => S = s);
		ValueField = TextField<float>.Float01(_v, "Value", v => V = v);
		Alpha1Field = TextField<float>.Float01(tempColour.a, "Alpha1", a => A = a);
		RedField = TextField<float>.Float01(tempColour.r, "Red", r => R = r);
		GreenField = TextField<float>.Float01(tempColour.g, "Green", g => G = g);
		BlueField = TextField<float>.Float01(tempColour.b, "Blue", b => B = b);
		Alpha2Field = TextField<float>.Float01(tempColour.a, "Alpha2", a => A = a);
		HexField = TextField<string>.Hex(Hex, "Hex", hex => Hex = hex);
		_textFieldIds = ["Hue", "Saturation", "Value", "Alpha1", "Red", "Green", "Blue", "Alpha2", "Hex"];
		NotifyRGBUpdated();
	}

	public override void PreOpen()
	{
		base.PreOpen();
		NotifyHSVUpdated();
	}

	public override void Close(bool doCloseSound = true)
	{
		base.Close(doCloseSound);
		pawn.drawer.renderer.SetAllGraphicsDirty();
	}

	public override void OnAcceptKeyPressed()
	{
		base.OnAcceptKeyPressed();
		Accept();
	}

	public float A
	{
		get => tempColour.a;
		set { Color c = tempColour; c.a = Mathf.Clamp(value, 0f, 1f); tempColour = c; NotifyRGBUpdated(); }
	}

	public float R
	{
		get => tempColour.r;
		set { Color c = tempColour; c.r = Mathf.Clamp(value, 0f, 1f); tempColour = c; NotifyRGBUpdated(); }
	}

	public float G
	{
		get => tempColour.g;
		set { Color c = tempColour; c.g = Mathf.Clamp(value, 0f, 1f); tempColour = c; NotifyRGBUpdated(); }
	}

	public float B
	{
		get => tempColour.b;
		set { Color c = tempColour; c.b = Mathf.Clamp(value, 0f, 1f); tempColour = c; NotifyRGBUpdated(); }
	}

	public float H
	{
		get => _h;
		set { _h = Mathf.Clamp(value, 0f, 1f); NotifyHSVUpdated(); CreateColourPickerBg(); CreateAlphaPickerBg(); }
	}

	public float S
	{
		get => _s;
		set { _s = Mathf.Clamp(value, 0f, 1f); NotifyHSVUpdated(); CreateAlphaPickerBg(); }
	}

	public float V
	{
		get => _v;
		set { _v = Mathf.Clamp(value, 0f, 1f); NotifyHSVUpdated(); CreateAlphaPickerBg(); }
	}

	public string Hex
	{
		get => $"#{ColorUtility.ToHtmlStringRGBA(tempColour)}";
		set { _hex = value; NotifyHexUpdated(); }
	}

	public float UnitsPerPixel
	{
		get
		{
			if (field == 0f)
				field = 1f / _pickerSize;
			return field;
		}
	}

	public Texture2D ColourPickerBG { get { if (_colourPickerBG == null) CreateColourPickerBg(); return _colourPickerBG!; } }
	public Texture2D HuePickerBG { get { if (_huePickerBG == null) CreateHuePickerBg(); return _huePickerBG!; } }
	public Texture2D AlphaPickerBG { get { if (_alphaPickerBG == null) CreateAlphaPickerBg(); return _alphaPickerBG!; } }
	public Texture2D TempPreviewBG { get { if (_tempPreviewBG == null) CreatePreviewBg(ref _tempPreviewBG, tempColour); return _tempPreviewBG!; } }
	public Texture2D PreviewBG { get { if (_previewBG == null) CreatePreviewBg(ref _previewBG, curColour); return _previewBG!; } }
	public Texture2D PickerAlphaBG { get { if (field == null) CreateAlphaBg(ref field, _pickerSize, _pickerSize); return field!; } }
	public Texture2D PreviewAlphaBG { get { if (field == null) CreateAlphaBg(ref field, _previewSize, _previewSize); return field!; } }
	public Texture2D SliderAlphaBG { get { if (field == null) CreateAlphaBg(ref field, _sliderWidth, _pickerSize); return field!; } }

	public override void DoWindowContents(Rect inRect)
	{
		using (TextBlock.Default())
		{
			// Live-bind the picker's current colour to the selected pattern slot so the portrait
			// preview tracks any picker interaction this frame.
			_selectedColors[_selectedPattern] = tempColour;

			var portraitRect = new Rect(inRect.x, inRect.y, PortraitWidth, PortraitHeight);
			DrawPortrait(portraitRect);

			float pickerX = inRect.x + PortraitWidth + ColumnGap;
			var pickerInRect = new Rect(pickerX, inRect.y, inRect.xMax - pickerX, _pickerSize);
			DrawPicker(pickerInRect);

			var dropdownRect = new Rect(pickerX, pickerInRect.yMax + _margin, 220f, PatternDropdownHeight);
			DrawPatternDropdown(dropdownRect);

			var geneDropdownRect = new Rect(dropdownRect.xMax + _margin, dropdownRect.y, 280f, PatternDropdownHeight);
			DrawGeneDropdown(geneDropdownRect);
		}
	}

	private void DrawPortrait(Rect portrait)
	{
		Widgets.DrawMenuSection(portrait);

		Color[] oldColors = new Color[ColorCount];
		for (int i = Skin; i < ColorCount; ++i)
		{
			oldColors[i] = Ot(i, _geneFurPatterns);
			if (_geneFurPatterns[i] != null)
				Rt(i, _geneFurPatterns!) = _selectedColors[i];
		}
		var originalSkinColorOverride = pawn.story.skinColorOverride;
		pawn.story.skinColorOverride = _geneFurPatterns[Skin]!.colorOne;
		pawn.drawer.renderer.SetAllGraphicsDirty();
		var image = PortraitsCache.Get(pawn, new(400, 400), _pawnFacingDirection, new(0, 0, 0.1f), healthStateOverride: PawnHealthState.Mobile, cameraZoom: 1.1f, renderClothes: false, renderHeadgear: false);
		for (int i = Skin; i < ColorCount; ++i)
		{
			if (_geneFurPatterns[i] != null)
				Rt(i, _geneFurPatterns!) = oldColors[i];
		}
		pawn.story.skinColorOverride = originalSkinColorOverride;
		pawn.drawer.renderer.SetAllGraphicsDirty();

		GUI.DrawTexture(portrait, image, ScaleMode.ScaleAndCrop);
		var buttonRotate = new Rect(portrait.xMax - 24, portrait.y, 24, 24);
		if (Widgets.ButtonImage(buttonRotate, s_rotateButton))
			_pawnFacingDirection = _pawnFacingDirection.Rotated(RotationDirection.Clockwise);
	}

	private static string PatternKey(int i) => i switch
	{
		BodyFill => nameof(BodyFill),
		BodyAccent => nameof(BodyAccent),
		HeadFill => nameof(HeadFill),
		HeadAccent => nameof(HeadAccent),
		EarsFill => nameof(EarsFill),
		EarsAccent => nameof(EarsAccent),
		TailFill => nameof(TailFill),
		TailAccent => nameof(TailAccent),
		_ => nameof(Skin),
	};

	private void DrawPatternDropdown(Rect rect)
	{
		var current = $"ColorPicker.{PatternKey(_selectedPattern)}".Translate();
		if (Widgets.ButtonText(rect, current))
		{
			var options = new List<FloatMenuOption>(ColorCount);
			for (int i = Skin; i < ColorCount; ++i)
			{
				int captured = i;
				options.Add(new($"ColorPicker.{PatternKey(i)}".Translate(), () => SelectPattern(captured)));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}

	private void SelectPattern(int i)
	{
		if (i == _selectedPattern)
			return;
		_selectedColors[_selectedPattern] = tempColour;
		_selectedPattern = i;
		tempColour = _selectedColors[i];
		curColour = _originalColors[i];
		NotifyRGBUpdated();
		CreatePreviewBg(ref _previewBG, curColour);
	}

	/// <summary>
	/// The gene C# generic type that owns a given pattern slot. Skin slot has no real gene (it's a synthetic wrapper for skinColorOverride), and EarsFill/EarsAccent (resp. TailFill/TailAccent) share one gene.
	/// </summary>
	private static Type? SlotGeneType(int slot) => slot switch
	{
		BodyFill => typeof(GeneFurPatternFill),
		BodyAccent => typeof(GeneFurPatternAccent),
		HeadFill => typeof(GeneHeadPatternFill),
		HeadAccent => typeof(GeneHeadPatternAccent),
		EarsFill or EarsAccent => typeof(GeneEarsWithPattern),
		TailFill or TailAccent => typeof(GeneTailWithPattern),
		_ => null,
	};

	/// <summary>
	/// Ears and tail genes are required (a Cheech without ears or a tail is just sad); body and head fill/accent are optional and the user is allowed to remove them via the "(none)" option.
	/// </summary>
	private static bool IsOptionalSlot(int slot) => slot is BodyFill or BodyAccent or HeadFill or HeadAccent;

	private void DrawGeneDropdown(Rect rect)
	{
		var slotType = SlotGeneType(_selectedPattern);
		if (slotType == null)
		{
			GUI.color = Color.gray;
			Widgets.ButtonText(rect, "ColorPicker.GeneNotApplicable".Translate(), drawBackground: true, doMouseoverSound: false, active: false);
			GUI.color = Color.white;
			return;
		}

		var currentGene = _geneFurPatterns[_selectedPattern];
		string label = currentGene?.def.LabelCap.RawText ?? currentGene?.def.defName ?? "ColorPicker.GeneNone".Translate().RawText;

		if (Widgets.ButtonText(rect, label))
		{
			var options = new List<FloatMenuOption>();
			var candidates = DefDatabase<GeneDef>.AllDefsListForReading.Where(d => d.geneClass == slotType).OrderBy(d => d.LabelCap.RawText ?? d.defName);

			foreach (var def in candidates)
			{
				var captured = def;
				string optionLabel = captured.LabelCap.RawText ?? captured.defName;
				options.Add(new(optionLabel, () => SwapGene(captured)));
			}

			if (IsOptionalSlot(_selectedPattern))
				options.Add(new("ColorPicker.GeneNone".Translate(), () => SwapGene(null)));

			if (options.Count > 0)
				Find.WindowStack.Add(new FloatMenu(options));
		}
	}

	/// <summary>
	/// Replaces the gene currently filling the selected slot. Applied immediately to the pawn; Cancel does not roll back gene-swaps the way it rolls back color edits because it's more code that might not be worth it.
	/// </summary>
	private void SwapGene(GeneDef? newDef)
	{
		int slot = _selectedPattern;
		var slotType = SlotGeneType(slot);
		if (slotType == null)
			return;

		var oldGene = _geneFurPatterns[slot];
		if (oldGene != null && oldGene.def == newDef)
			return;

		// Carrying the user's current picker color across the swap. For the shared ears/tail genes, preserving both halves; for the rest, only colorOne is meaningful.
		_selectedColors[slot] = tempColour;
		Color preservedOne, preservedTwo;
		if (slotType == typeof(GeneEarsWithPattern))
		{
			preservedOne = _selectedColors[EarsFill];
			preservedTwo = _selectedColors[EarsAccent];
		}
		else if (slotType == typeof(GeneTailWithPattern))
		{
			preservedOne = _selectedColors[TailFill];
			preservedTwo = _selectedColors[TailAccent];
		}
		else
		{
			preservedOne = _selectedColors[slot];
			preservedTwo = default;
		}

		// Since it's not part of the original coat theme, we're electing to default to a xenogene when the gene wasn't otherwise present. scradam isn't sure if he likes it best this way or not, because it might convert endogenes to xenogenes when the user selects 'none' and then re-selects another gene.
		bool xenogene = oldGene == null || pawn.genes.Xenogenes.Contains(oldGene);

		if (oldGene != null)
			pawn.genes.RemoveGene(oldGene);

		if (newDef != null && pawn.genes.AddGene(newDef, xenogene) is GeneFurPattern newGene && oldGene != null)
		{
			// Inheriting the user's picks. If there was no old gene, leave PostAdd's theme-rolled colors alone.
			newGene.colorOne = preservedOne;
			newGene.colorTwo = preservedTwo;
		}

		// Refresh slot → gene mapping.
		_geneFurPatterns[BodyFill] = pawn.GetGene<GeneFurPatternFill>();
		_geneFurPatterns[BodyAccent] = pawn.GetGene<GeneFurPatternAccent>();
		_geneFurPatterns[HeadFill] = pawn.GetGene<GeneHeadPatternFill>();
		_geneFurPatterns[HeadAccent] = pawn.GetGene<GeneHeadPatternAccent>();
		_geneFurPatterns[EarsAccent] = _geneFurPatterns[EarsFill] = pawn.GetGene<GeneEarsWithPattern>();
		_geneFurPatterns[TailAccent] = _geneFurPatterns[TailFill] = pawn.GetGene<GeneTailWithPattern>();

		// Reseeding selectedColors from the (possibly new) gene state, and re-syncing the picker.
		for (int i = Skin; i < ColorCount; ++i)
			_selectedColors[i] = Ot(i, _geneFurPatterns);

		tempColour = _selectedColors[slot];
		curColour = _selectedColors[slot];
		NotifyRGBUpdated();
		CreatePreviewBg(ref _previewBG, curColour);

		RebuildRenderTreeForGeneChange();
	}

	/// <summary>
	/// Forces a synchronous render-tree rebuild + portrait-cache flush after a gene swap. The actual child-ordering correction is handled at the source by <see cref="HarmonyPatch_DynamicPawnRenderNodeSetup_Genes_GetDynamicNodes"/>,
	/// which stable-sorts gene render nodes by <c>baseLayer</c> across both <c>xenogenes</c> and <c>endogenes</c>. That patch fires every time the tree rebuilds, so we just need to make sure the rebuild *happens* before the next
	/// portrait fetch — <c>PawnRenderer.SetAllGraphicsDirty</c> alone won't do it, because it defers through <c>LongEventHandler.ExecuteWhenFinished</c> and bails if the tree isn't <c>Resolved</c>. We call the underlying dirty-marks
	/// synchronously and still chain <c>SetAllGraphicsDirty</c> so silhouette + global-texture-atlas caches catch up too.
	/// </summary>
	private void RebuildRenderTreeForGeneChange()
	{
		// Synchronous. PawnRenderer.SetAllGraphicsDirty defers via LongEventHandler and bails if the tree isn't Resolved, so it can't be relied on to land before the next portrait fetch.
		pawn.drawer.renderer.renderTree.SetDirty();
		PortraitsCache.SetDirty(pawn);
		// Still calling this so silhouette + global texture atlas caches get flushed too.
		pawn.drawer.renderer.SetAllGraphicsDirty();
	}

	private void Accept()
	{
		_selectedColors[_selectedPattern] = tempColour;
		for (int i = Skin; i < ColorCount; ++i)
		{
			if (_geneFurPatterns[i] != null && _selectedColors[i] != _originalColors[i])
				Rt(i, _geneFurPatterns!) = _selectedColors[i];
		}
		if (pawn.story.skinColorOverride == null
			? pawn.story.SkinColorBase != _geneFurPatterns[Skin]!.colorOne
			: pawn.story.skinColorOverride != _geneFurPatterns[Skin]!.colorOne
		)
		{
			pawn.story.skinColorOverride = _geneFurPatterns[Skin]!.colorOne;
		}

		_recentColours.Add(tempColour);
		Close();
	}

	private void DrawPicker(Rect inRect)
	{
		Rect pickerRect = new(inRect.xMin, inRect.yMin, _pickerSize, _pickerSize);
		Rect hueRect = new(pickerRect.xMax + _margin, inRect.yMin, _sliderWidth, _pickerSize);
		Rect alphaRect = new(hueRect.xMax + _margin, inRect.yMin, _sliderWidth, _pickerSize);
		Rect previewRect = new(alphaRect.xMax + _margin, inRect.yMin, _previewSize, _previewSize);
		Rect previewOldRect = new(previewRect.xMax, inRect.yMin, _previewSize, _previewSize);
		Rect doneRect = new(alphaRect.xMax + _margin, inRect.yMax - _buttonHeight, (_previewSize * 2) - _margin - _previewSize, _buttonHeight);
		Rect cancelRect = new(doneRect.xMax + _margin, inRect.yMax - _buttonHeight, _previewSize, _buttonHeight);
		Rect hsvFieldRect = new(alphaRect.xMax + _margin, inRect.yMax - _buttonHeight - (3 * _fieldHeight) - (4 * _margin), _previewSize * 2, _fieldHeight);
		Rect rgbFieldRect = new(alphaRect.xMax + _margin, inRect.yMax - _buttonHeight - (2 * _fieldHeight) - (3 * _margin), _previewSize * 2, _fieldHeight);
		Rect hexRect = new(alphaRect.xMax + _margin, inRect.yMax - _buttonHeight - _fieldHeight - (2 * _margin), _previewSize * 2, _fieldHeight);
		Rect recentRect = new(previewRect.xMin, previewRect.yMax + _margin, _previewSize * 2, _recentSize * 2);

		GUI.DrawTexture(pickerRect, PickerAlphaBG);
		GUI.DrawTexture(alphaRect, SliderAlphaBG);
		GUI.DrawTexture(previewRect, PreviewAlphaBG);
		GUI.DrawTexture(previewOldRect, PreviewAlphaBG);

		GUI.DrawTexture(pickerRect, ColourPickerBG);
		GUI.DrawTexture(hueRect, HuePickerBG);
		GUI.DrawTexture(alphaRect, AlphaPickerBG);
		GUI.DrawTexture(previewRect, TempPreviewBG);
		GUI.DrawTexture(previewOldRect, PreviewBG);

		if (Widgets.ButtonInvisible(previewOldRect))
		{
			tempColour = curColour;
			NotifyRGBUpdated();
		}

		DrawRecent(recentRect);

		Rect hueHandleRect = new(hueRect.xMin - 3f, hueRect.yMin + _huePosition - (_handleSize / 2), _sliderWidth + 6f, _handleSize);
		Rect alphaHandleRect = new(alphaRect.xMin - 3f, alphaRect.yMin + _alphaPosition - (_handleSize / 2), _sliderWidth + 6f, _handleSize);
		Rect pickerHandleRect = new(pickerRect.xMin + _position.x - (_handleSize / 2), pickerRect.yMin + _position.y - (_handleSize / 2), _handleSize, _handleSize);
		GUI.DrawTexture(hueHandleRect, TempPreviewBG);
		GUI.DrawTexture(alphaHandleRect, TempPreviewBG);
		GUI.DrawTexture(pickerHandleRect, TempPreviewBG);

		GUI.color = Color.gray;
		Widgets.DrawBox(hueHandleRect);
		Widgets.DrawBox(alphaHandleRect);
		Widgets.DrawBox(pickerHandleRect);
		GUI.color = Color.white;

		if (Input.GetMouseButtonUp(0))
			_activeControl = Controls.None;

		DrawColourPicker(pickerRect);
		DrawHuePicker(hueRect);
		DrawAlphaPicker(alphaRect);
		DrawFields(hsvFieldRect, rgbFieldRect, hexRect);
		DrawButtons(doneRect, cancelRect);

		GUI.color = Color.white;
	}

	private void DrawAlphaPicker(Rect alphaRect)
	{
		if (Mouse.IsOver(alphaRect))
		{
			if (Input.GetMouseButtonDown(0))
				_activeControl = Controls.AlphaPicker;

			if (Event.current.type == EventType.ScrollWheel)
			{
				A -= Event.current.delta.y * UnitsPerPixel;
				_alphaPosition = Mathf.Clamp(_alphaPosition + Event.current.delta.y, 0f, _pickerSize);
				Event.current.Use();
			}

			if (_activeControl == Controls.AlphaPicker)
			{
				float mousePosition = Event.current.mousePosition.y;
				float positionInRect = mousePosition - alphaRect.yMin;
				AlphaAction(positionInRect);
			}
		}
	}

	private void DrawButtons(Rect doneRect, Rect cancelRect)
	{
		if (Widgets.ButtonText(doneRect, "OK".Translate()))
			Accept();

		if (Widgets.ButtonText(cancelRect, "Cancel".Translate()))
			Close();
	}

	private void DrawColourPicker(Rect pickerRect)
	{
		if (Mouse.IsOver(pickerRect))
		{
			if (Input.GetMouseButtonDown(0))
				_activeControl = Controls.ColourPicker;

			if (_activeControl == Controls.ColourPicker)
			{
				Vector2 mousePosition = Event.current.mousePosition;
				Vector2 positionInRect = mousePosition - new Vector2(pickerRect.xMin, pickerRect.yMin);
				PickerAction(positionInRect);
			}
		}
	}

	private void DrawFields(Rect hsvFieldRect, Rect rgbFieldRect, Rect hexRect)
	{
		Text.Font = GameFont.Small;

		Rect fieldRect = hsvFieldRect;
		fieldRect.width /= 5f;
		Text.Font = GameFont.Tiny;
		Text.Anchor = TextAnchor.MiddleCenter;
		GUI.color = Color.grey;
		Widgets.Label(fieldRect, "HSV");
		Text.Font = GameFont.Small;
		GUI.color = Color.white;
		fieldRect.x += fieldRect.width;
		HueField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		SaturationField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		ValueField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		Alpha1Field.Draw(fieldRect);

		fieldRect = rgbFieldRect;
		fieldRect.width /= 5f;
		Text.Font = GameFont.Tiny;
		GUI.color = Color.grey;
		Widgets.Label(fieldRect, "RGB");
		Text.Font = GameFont.Small;
		GUI.color = Color.white;
		fieldRect.x += fieldRect.width;
		RedField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		GreenField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		BlueField.Draw(fieldRect);
		fieldRect.x += fieldRect.width;
		Alpha2Field.Draw(fieldRect);

		Text.Font = GameFont.Tiny;
		GUI.color = Color.grey;
		Widgets.Label(new(hexRect.xMin, hexRect.yMin, fieldRect.width, hexRect.height), "HEX");
		Text.Font = GameFont.Small;
		GUI.color = Color.white;
		hexRect.xMin += fieldRect.width;
		HexField.Draw(hexRect);
		Text.Anchor = TextAnchor.UpperLeft;

		if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
		{
			string curControl = GUI.GetNameOfFocusedControl();
			int curControlIndex = _textFieldIds.IndexOf(curControl);
			GUI.FocusControl(_textFieldIds[
				GenMath.PositiveMod(curControlIndex + (Event.current.shift ? -1 : 1),
					_textFieldIds.Count)]);
		}
	}

	private void DrawHuePicker(Rect hueRect)
	{
		if (Mouse.IsOver(hueRect))
		{
			if (Input.GetMouseButtonDown(0))
				_activeControl = Controls.HuePicker;

			if (Event.current.type == EventType.ScrollWheel)
			{
				H -= Event.current.delta.y * UnitsPerPixel;
				_huePosition = Mathf.Clamp(_huePosition + Event.current.delta.y, 0f, _pickerSize);
				Event.current.Use();
			}

			if (_activeControl == Controls.HuePicker)
			{
				float mousePosition = Event.current.mousePosition.y;
				float positionInRect = mousePosition - hueRect.yMin;
				HueAction(positionInRect);
			}
		}
	}

	private void DrawRecent(Rect canvas)
	{
		int cols = (int)(canvas.width / _recentSize);
		int rows = (int)(canvas.height / _recentSize);
		int n = Math.Min(cols * rows, _recentColours.Count);

		GUI.BeginGroup(canvas);
		for (int i = 0; i < n; i++)
		{
			int col = i % cols;
			int row = i / cols;
			Color color = _recentColours[i];
			Rect rect = new(col * _recentSize, row * _recentSize, _recentSize, _recentSize);
			Widgets.DrawBoxSolid(rect, color);
			if (Mouse.IsOver(rect))
				Widgets.DrawBox(rect);

			if (Widgets.ButtonInvisible(rect))
			{
				tempColour = color;
				NotifyRGBUpdated();
			}
		}
		GUI.EndGroup();
	}

	private void CreateAlphaBg(ref Texture2D? bg, int width, int height)
	{
		Texture2D tex = new(width + _alphaBGBlockSize, height + _alphaBGBlockSize);

		Color[] bgA = new Color[_alphaBGBlockSize * _alphaBGBlockSize];
		for (int i = 0; i < bgA.Length; i++)
			bgA[i] = _alphaBGColorA;

		Color[] bgB = new Color[_alphaBGBlockSize * _alphaBGBlockSize];
		for (int i = 0; i < bgB.Length; i++)
			bgB[i] = _alphaBGColorB;

		int row = 0;
		for (int x = 0; x < width; x += _alphaBGBlockSize)
		{
			int column = row;
			for (int y = 0; y < height; y += _alphaBGBlockSize)
			{
				tex.SetPixels(x, y, _alphaBGBlockSize, _alphaBGBlockSize, column % 2 == 0 ? bgA : bgB);
				column++;
			}
			row++;
		}

		tex.Apply();
		SwapTexture(ref bg, tex);
	}

	private void CreateAlphaPickerBg()
	{
		Texture2D tex = new(1, _pickerSize);
		const float hu = 1f / _pickerSize;
		for (int y = 0; y < _pickerSize; y++)
			tex.SetPixel(0, y, new(tempColour.r, tempColour.g, tempColour.b, y * hu));
		tex.Apply();
		SwapTexture(ref _alphaPickerBG, tex);
	}

	private void CreateColourPickerBg()
	{
		float wu = UnitsPerPixel;
		float hu = UnitsPerPixel;

		Texture2D tex = new(_pickerSize, _pickerSize);
		for (int x = 0; x < _pickerSize; x++)
		{
			for (int y = 0; y < _pickerSize; y++)
			{
				float iS = x * wu;
				float iV = y * hu;
				tex.SetPixel(x, y, HsvaToRgb(H, iS, iV, A));
			}
		}
		tex.Apply();
		SwapTexture(ref _colourPickerBG, tex);
	}

	private void CreateHuePickerBg()
	{
		Texture2D tex = new(1, _pickerSize);
		const float hu = 1f / _pickerSize;
		for (int y = 0; y < _pickerSize; y++)
			tex.SetPixel(0, y, Color.HSVToRGB(hu * y, 1f, 1f));
		tex.Apply();
		SwapTexture(ref _huePickerBG, tex);
	}

	public void CreatePreviewBg(ref Texture2D? bg, Color col) => SwapTexture(ref bg, SolidColorMaterials.NewSolidColorTexture(col));

	public static Color HsvaToRgb(float h, float s, float v, float a)
	{
		Color color = Color.HSVToRGB(h, s, v);
		color.a = a;
		return color;
	}

	public void AlphaAction(float pos)
	{
		A = 1 - (UnitsPerPixel * pos);
		_alphaPosition = pos;
	}

	public void HueAction(float pos)
	{
		H = 1 - (UnitsPerPixel * pos);
		_huePosition = pos;
	}

	public void PickerAction(Vector2 pos)
	{
		_s = UnitsPerPixel * pos.x;
		_v = 1 - (UnitsPerPixel * pos.y);

		CreateAlphaPickerBg();
		NotifyHSVUpdated();
		_position = pos;
	}

	public void NotifyHexUpdated()
	{
		if (ColorUtility.TryParseHtmlString(_hex, out Color color))
		{
			tempColour = color;
			NotifyRGBUpdated();
			RedField.Value = tempColour.r;
			GreenField.Value = tempColour.g;
			BlueField.Value = tempColour.b;
		}
	}

	public void NotifyHSVUpdated()
	{
		Color color = Color.HSVToRGB(H, S, V);
		color.a = A;
		tempColour = color;

		CreatePreviewBg(ref _tempPreviewBG, tempColour);
		SetPickerPositions();

		RedField.Value = tempColour.r;
		GreenField.Value = tempColour.g;
		BlueField.Value = tempColour.b;
		HueField.Value = H;
		SaturationField.Value = S;
		ValueField.Value = V;
		Alpha1Field.Value = A;
		Alpha2Field.Value = A;
		HexField.Value = Hex;
	}

	public void NotifyRGBUpdated()
	{
		Color.RGBToHSV(tempColour, out _h, out _s, out _v);

		CreateColourPickerBg();
		CreateHuePickerBg();
		CreateAlphaPickerBg();

		CreatePreviewBg(ref _tempPreviewBG, tempColour);
		SetPickerPositions();

		HueField.Value = H;
		SaturationField.Value = S;
		ValueField.Value = V;
		Alpha1Field.Value = A;
		Alpha2Field.Value = A;
		HexField.Value = Hex;
	}

	public void SetPickerPositions()
	{
		_huePosition = (1f - H) / UnitsPerPixel;
		_position.x = S / UnitsPerPixel;
		_position.y = (1f - V) / UnitsPerPixel;
		_alphaPosition = (1f - A) / UnitsPerPixel;
	}

	private static void SwapTexture(ref Texture2D? tex, Texture2D newTex)
	{
		if (tex != null)
			UnityEngine.Object.Destroy(tex);
		tex = newTex;
	}

	private enum Controls
	{
		ColourPicker,
		HuePicker,
		AlphaPicker,
		None,
	}
}
