using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Data;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Builds a card out of nothing and shows what it would look like.
    ///
    /// A development tool, not a card creator for players and not the beginning
    /// of a deck builder. It exists to answer one question quickly — what does
    /// this combination compose to, and what is still missing — without loading
    /// a scene or dealing a hand.
    ///
    /// The important part is what it does not contain: any drawing. It builds a
    /// <see cref="CardVisualDescriptor"/> and asks the same
    /// <see cref="CardVisualFactory"/> the game asks, gets the same
    /// <see cref="CardVisualPlan"/>, and hands it to the same
    /// <see cref="CardVisualPainter"/> on a hidden object. A preview is
    /// therefore not an approximation of what the game will draw. It is the
    /// same composition, rendered by the same code, and the two cannot drift
    /// apart because there is only one of them.
    /// </summary>
    public sealed class CardVisualPreviewWindow : EditorWindow
    {
        private CardVisualFactory _factory;

        private CardType _type = CardType.Minion;
        private CardClass _class = CardClass.Neutral;
        private Rarity _rarity = Rarity.Common;
        private Tribe _tribe = Tribe.None;
        private Sprite _artwork;
        private string _name = "Test Soldier";
        private string _rules = "";
        private int _cost = 2;
        private int _attack = 2;
        private int _health = 3;
        private bool _faceDown;

        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private GameObject _stage;
        private CardVisualPainter _painter;
        private Camera _camera;
        private RenderTexture _target;

        private Vector2 _scroll;
        private bool _showLayers = true;

        /// <summary>
        /// Shows a minion and a spell at once, so the two title styles can be
        /// compared rather than remembered.
        ///
        /// The whole claim the composer makes about titles is that a minion and
        /// a spell are the same renderer given different numbers. Two cards side
        /// by side is how that claim is checked: if they ever stop differing in
        /// the ways the recipe says and start differing in some other way,
        /// something has grown a branch it should not have.
        /// </summary>
        private bool _compare;

        // --- polishing one card by hand ------------------------------------
        private CardDefinitionAsset _definition;
        private bool _rawCurve;

        // --- laying the labels out by hand ---------------------------------
        private bool _editing;
        private CardVisualTextSlot _slot = CardVisualTextSlot.Name;
        private int _snap = 1;

        private Rect _baseline;
        private bool _hasBaseline;

        private int _grabbed = -1;
        private Vector2 _grabbedAt;
        private Rect _grabbedRect;

        /// <summary>
        /// How much of the preview the card fills.
        ///
        /// The camera frames a little more than the card so its edge is
        /// visible, and the handles have to land on the card rather than on the
        /// texture. One constant, used by the camera and by the arithmetic that
        /// turns a canvas coordinate into a pixel on screen, so the two cannot
        /// disagree.
        /// </summary>
        private const float Framing = 0.52f;

        private static float CardFraction => 0.5f / Framing;

        [MenuItem("Tools/Conquest of Hearthstone/Card Visual Preview")]
        public static void Open()
        {
            CardVisualPreviewWindow window = GetWindow<CardVisualPreviewWindow>("Card Visual");
            window.minSize = new Vector2(560f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_factory == null)
            {
                _factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            TearDownStage();
        }

        /// <summary>
        /// An undo rewinds the recipe asset, so the card has to be composed
        /// again from it. Without this the window would keep showing the layout
        /// that was just undone.
        /// </summary>
        private void OnUndoRedo()
        {
            _hasBaseline = false;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawControls();
                DrawPreview();
            }
        }

        // ------------------------------------------------------------------
        //  The card being described
        // ------------------------------------------------------------------

        private void DrawControls()
        {
            using (EditorGUILayout.ScrollViewScope scroll =
                new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Width(340f)))
            {
                _scroll = scroll.scrollPosition;

                _factory = (CardVisualFactory)EditorGUILayout.ObjectField(
                    "Factory", _factory, typeof(CardVisualFactory), false);

                EditorGUILayout.Space();

                _type = (CardType)EditorGUILayout.EnumPopup("Type", _type);
                _class = (CardClass)EditorGUILayout.EnumPopup("Class", _class);
                _rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", _rarity);
                _tribe = (Tribe)EditorGUILayout.EnumPopup("Tribe", _tribe);

                EditorGUILayout.Space();

                _artwork = (Sprite)EditorGUILayout.ObjectField("Artwork", _artwork, typeof(Sprite), false);
                _name = EditorGUILayout.TextField("Name", _name);

                EditorGUILayout.LabelField("Rules Text");
                _rules = EditorGUILayout.TextArea(_rules, GUILayout.Height(48f));

                EditorGUILayout.Space();

                _cost = EditorGUILayout.IntField("Mana", _cost);
                _attack = EditorGUILayout.IntField("Attack", _attack);
                _health = EditorGUILayout.IntField("Health", _health);

                EditorGUILayout.Space();
                _faceDown = EditorGUILayout.Toggle("Face down", _faceDown);

                bool compare = EditorGUILayout.Toggle("Compare minion and spell", _compare);

                if (compare != _compare)
                {
                    _compare = compare;

                    // Handles belong to one card. With two on screen there is no
                    // "the" rectangle to drag.
                    if (_compare)
                    {
                        _editing = false;
                    }
                }

                EditorGUILayout.Space();
                DrawCardChoice();

                EditorGUILayout.Space();
                DrawLayoutEditor();

                EditorGUILayout.Space();
                DrawReport();
            }
        }

        // ------------------------------------------------------------------
        //  Laying the labels out by hand
        // ------------------------------------------------------------------

        /// <summary>
        /// The layer this card would use for a text slot, or null.
        ///
        /// The *layer*, not the composed result: editing has to change the
        /// recipe, because the composed card is thrown away and rebuilt every
        /// repaint. And it has to be the layer that applies to the card on
        /// screen, which is what keeps a minion's name slot and a spell's name
        /// slot independent without either of them being named anywhere here.
        /// </summary>
        private CardVisualLayerDefinition EditableLayer(out CardVisualRecipeAsset recipe)
        {
            recipe = null;

            if (_factory == null)
            {
                return null;
            }

            CardVisualDescriptor card = Describe();
            recipe = _factory.RecipeFor(card.Style);

            if (recipe == null)
            {
                return null;
            }

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer != null && layer.text == _slot && layer.AppliesTo(card))
                {
                    return layer;
                }
            }

            return null;
        }

        private void DrawLayoutEditor()
        {
            _editing = EditorGUILayout.ToggleLeft("Layout editing", _editing, EditorStyles.boldLabel);

            if (!_editing)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                CardVisualTextSlot wanted = (CardVisualTextSlot)EditorGUILayout.EnumPopup("Slot", _slot);

                if (wanted != _slot)
                {
                    _slot = wanted;
                    _hasBaseline = false;
                }

                CardVisualLayerDefinition layer = EditableLayer(out CardVisualRecipeAsset recipe);

                if (layer == null)
                {
                    EditorGUILayout.HelpBox(
                        "This card has no " + _slot + " layer. Change the card type, or add the layer " +
                        "to the recipe.", MessageType.Info);
                    return;
                }

                if (!_hasBaseline)
                {
                    _baseline = new Rect(layer.x, layer.y, layer.width, layer.height);
                    _hasBaseline = true;
                }

                EditorGUILayout.LabelField(layer.name, EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();

                float x = EditorGUILayout.FloatField("X", layer.x);
                float y = EditorGUILayout.FloatField("Y", layer.y);
                float width = EditorGUILayout.FloatField("Width", layer.width);
                float height = EditorGUILayout.FloatField("Height", layer.height);

                EditorGUILayout.Space();

                float ceiling = EditorGUILayout.FloatField("Font size max", layer.fontSize);
                float floor = EditorGUILayout.FloatField("Font size min", layer.fontSizeMin);
                bool wrap = EditorGUILayout.Toggle("Wraps", layer.wrap);

                CardVisualAlignment alignment =
                    (CardVisualAlignment)EditorGUILayout.EnumPopup("Alignment", layer.alignment);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(recipe, "Lay out " + _slot);

                    layer.x = x;
                    layer.y = y;
                    layer.width = Mathf.Max(1f, width);
                    layer.height = Mathf.Max(1f, height);
                    layer.fontSize = ceiling;
                    layer.fontSizeMin = floor;
                    layer.wrap = wrap;
                    layer.alignment = alignment;

                    EditorUtility.SetDirty(recipe);
                }

                DrawTextStyleEditor(recipe, layer);

                EditorGUILayout.Space();
                _snap = Mathf.Max(1, EditorGUILayout.IntField("Snap, canvas pixels", _snap));

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset"))
                    {
                        Undo.RecordObject(recipe, "Reset " + _slot);

                        layer.x = _baseline.x;
                        layer.y = _baseline.y;
                        layer.width = _baseline.width;
                        layer.height = _baseline.height;

                        EditorUtility.SetDirty(recipe);
                    }

                    if (GUILayout.Button("Save recipe"))
                    {
                        AssetDatabase.SaveAssets();
                        _hasBaseline = false;
                    }
                }

                EditorGUILayout.HelpBox(
                    "Drag inside the outline to move it, or a corner to resize. Reset returns to the " +
                    "values this slot had when it was selected; Save writes the recipe to disk. " +
                    "Style changes apply to every label set in that style. Author Card Text Styles " +
                    "leaves all of this alone, and so does Create Missing Card Visual Assets once " +
                    "the recipe is authored; only the explicitly destructive Danger command " +
                    "overwrites it.",
                    MessageType.None);
            }
        }

        /// <summary>
        /// Edits the style this label is set in, rather than the label.
        ///
        /// The distinction matters. A rectangle belongs to one layer; a style is
        /// shared by every layer that names it, so tuning the stretch on a
        /// minion title tunes the minion title and nothing else, while tuning
        /// the stat style tunes mana, attack and health together — which is the
        /// behaviour anybody nudging numbers into gems actually wants.
        /// </summary>
        private void DrawTextStyleEditor(
            CardVisualRecipeAsset recipe,
            CardVisualLayerDefinition layer)
        {
            EditorGUILayout.Space();

            CardTextStyleDefinition style = recipe.FindTextStyle(layer.textStyle);

            if (style == null)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(layer.textStyle)
                        ? "This label names no text style, so it is drawn plainly in the font its " +
                          "slot asks for. Run Author Card Text Styles to give the recipe its styles."
                        : "This label asks for the style '" + layer.textStyle +
                          "', which the recipe does not define.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Style: " + style.name, EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();

            CardTextRenderMode mode =
                (CardTextRenderMode)EditorGUILayout.EnumPopup("Render mode", style.renderMode);

            float outline = EditorGUILayout.Slider("Outline width", style.outlineWidth, 0f, 1f);
            float tracking = EditorGUILayout.FloatField("Tracking", style.tracking);

            float stretch = style.stretch;
            float taper = style.taper;
            Vector2 controlA = style.curveControlA;
            Vector2 controlB = style.curveControlB;
            Vector2 end = style.curveEnd;

            if (mode != CardTextRenderMode.Straight)
            {
                stretch = EditorGUILayout.Slider("Stretch", style.stretch, 0.5f, 3f);
                taper = EditorGUILayout.Slider("Taper", style.taper, 0f, 0.9f);

                DrawBaselineEditor(style, ref controlA, ref controlB, ref end);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(recipe, "Style " + style.name);

                style.renderMode = mode;
                style.outlineWidth = outline;
                style.tracking = tracking;
                style.stretch = stretch;
                style.taper = taper;
                style.curveControlA = controlA;
                style.curveControlB = controlB;
                style.curveEnd = end;

                EditorUtility.SetDirty(recipe);
            }
        }

        /// <summary>
        /// The baseline, in the terms somebody adjusting it thinks in: how deep
        /// the arch is, which way it leans, and where its top sits.
        ///
        /// The three control points are still the only thing stored. These
        /// sliders read them, and write them back through the same conversion,
        /// so there is one curve and not two descriptions of one that could
        /// drift apart. The raw points stay available underneath, because some
        /// shapes — the minion banner's lopsided S among them — cannot be said
        /// in three numbers, and the tool says so rather than quietly flattening
        /// them the first time a slider is touched.
        /// </summary>
        private void DrawBaselineEditor(
            CardTextStyleDefinition style,
            ref Vector2 controlA,
            ref Vector2 controlB,
            ref Vector2 end)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Baseline, in widths of the rectangle", EditorStyles.miniBoldLabel);

            CardTextCurve curve = CardTextCurve.From(
                style.curveControlA, style.curveControlB, style.curveEnd);

            bool describable = CardTextCurve.Fits(
                style.curveControlA, style.curveControlB, style.curveEnd);

            if (!describable)
            {
                EditorGUILayout.HelpBox(
                    "This baseline is not a plain arch — it rises at one end and falls at the " +
                    "other, which three numbers cannot describe. The figures below are the " +
                    "nearest arch to it, and moving any of them will replace the shape with " +
                    "that arch. The raw points below are unaffected until you do.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();

            float amount = EditorGUILayout.Slider("Curve amount", curve.Amount, -0.25f, 0.25f);
            float tilt = EditorGUILayout.Slider("Curve tilt", curve.Tilt, -0.25f, 0.25f);
            float centre = EditorGUILayout.Slider(
                "Curve centre",
                Mathf.Clamp(curve.Centre, CardTextCurve.NearestCentre, CardTextCurve.FurthestCentre),
                CardTextCurve.NearestCentre,
                CardTextCurve.FurthestCentre);

            if (EditorGUI.EndChangeCheck())
            {
                new CardTextCurve(amount, tilt, centre)
                    .ToControls(out controlA, out controlB, out end);
            }

            EditorGUILayout.LabelField(
                " ",
                "Amount arches upward; tilt raises the right hand end; centre is where the top " +
                "sits, and a single hump only reaches a third either way.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();
            _rawCurve = EditorGUILayout.Foldout(_rawCurve, "Control points (advanced)", true);

            if (!_rawCurve)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                controlA = EditorGUILayout.Vector2Field("Control A", controlA);
                controlB = EditorGUILayout.Vector2Field("Control B", controlB);
                end = EditorGUILayout.Vector2Field("End", end);

                EditorGUILayout.LabelField(
                    " ",
                    describable
                        ? "The three sliders above describe this curve exactly."
                        : "The sliders above cannot describe this curve.",
                    EditorStyles.miniLabel);
            }
        }

        // ------------------------------------------------------------------
        //  Polishing one card by hand
        // ------------------------------------------------------------------
        //
        // Moved out. This window used to carry a panel of eleven named fields a
        // card could differ on, which was useful and did not scale: every new
        // property meant another field here, and only text was reachable at all.
        //
        // Card Visual Editor authors any property the schema knows about, on any
        // layer, at whichever scope is selected. This window keeps what it is
        // still the better tool for - a quick look at a made up card, and
        // dragging a text box around by its corners.

        /// <summary>
        /// Which real card, if any, this window is showing.
        ///
        /// Kept because previewing an actual card is useful here too. What it no
        /// longer does is edit that card: adjusting one card's appearance is
        /// what Card Visual Editor is for, and having two windows that both
        /// wrote to the same data was a way to lose an edit.
        /// </summary>
        private void DrawCardChoice()
        {
            _definition = (CardDefinitionAsset)EditorGUILayout.ObjectField(
                "Real card", _definition, typeof(CardDefinitionAsset), false);

            if (_definition != null)
            {
                EditorGUILayout.HelpBox(
                    "Showing " + _definition.DisplayName + " with its own adjustments applied. " +
                    "Edit those in Card Visual Editor.", MessageType.None);
            }
        }

        private CardVisualDescriptor Describe() => Describe(_type);

        private CardVisualDescriptor Describe(CardType type)
        {
            // A real card, when one has been picked. Everything about it comes
            // from the asset the game reads, so what is previewed is the card
            // rather than a set of fields that resemble it — and its polish
            // comes from the library, exactly as it does in a match.
            if (_definition != null)
            {
                CoH.Core.Cards.CardDefinition card = _definition.ToDefinition();
                CardVisualLibraryAsset library = _factory != null ? _factory.Library : null;

                return new CardVisualDescriptor(
                    card.Type,
                    card.Class,
                    card.Rarity,
                    card.Tribe,
                    library != null ? library.ArtworkFor(card.Id) : _artwork,
                    card.Name,
                    card.Text,
                    card.ManaCost,
                    card.Attack,
                    card.Health,
                    showsCost: true,
                    showsStatistics: card.Type == CardType.Minion || card.Type == CardType.Weapon,
                    style: library != null ? library.StyleFor(card.Id) : default,
                    secondaryClass: CardClass.Neutral,
                    expansion: library != null ? library.ExpansionFor(card.Id) : string.Empty,
                    faceDown: _faceDown,
                    overrides: library != null ? library.OverridesFor(card.Id) : null);
            }

            return Made(type);
        }

        private CardVisualDescriptor Made(CardType type) =>
            new CardVisualDescriptor(
                type,
                _class,
                _rarity,
                _tribe,
                _artwork != null || _factory == null || _factory.Library == null
                    ? _artwork
                    : _factory.Library.ArtworkFor(default),
                _name,
                _rules,
                _cost,
                _attack,
                _health,
                showsCost: true,
                showsStatistics: type == CardType.Minion || type == CardType.Weapon,
                faceDown: _faceDown);

        // ------------------------------------------------------------------
        //  What came of it
        // ------------------------------------------------------------------

        private void DrawReport()
        {
            if (_factory == null)
            {
                EditorGUILayout.HelpBox(
                    "No factory. Run Conquest of Hearthstone → Create Missing Card Visual Assets.",
                    MessageType.Warning);
                return;
            }

            CardVisualDescriptor card = Describe();
            _factory.Compose(card, _plan);

            EditorGUILayout.LabelField(
                _plan.Layers.Count + " layer(s)", EditorStyles.miniBoldLabel);

            if (_plan.IsComplete)
            {
                EditorGUILayout.HelpBox("Composed completely.", MessageType.Info);
            }
            else
            {
                List<string> lines = new List<string>();

                for (int index = 0; index < _plan.Gaps.Count; index++)
                {
                    lines.Add(_plan.Gaps[index].Describe());
                }

                EditorGUILayout.HelpBox(
                    "Missing:\n" + string.Join("\n", lines), MessageType.Warning);
            }

            EditorGUILayout.Space();
            _showLayers = EditorGUILayout.Foldout(_showLayers, "Layers, and why each one applied", true);

            if (!_showLayers)
            {
                return;
            }

            // Read only, and deliberately the whole resolution rather than a
            // summary: which slot, which file it came from, where it sits and
            // the condition that let it through. Answering "why is that gem
            // wrong" should not require a debugger.
            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                CardVisualPlannedLayer layer = _plan.Layers[index];

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        layer.SortingOrder.ToString("D3") + "  " +
                        (layer.IsText ? layer.TextSlot.ToString() : layer.Slot.ToString()),
                        EditorStyles.miniBoldLabel);

                    if (layer.IsText)
                    {
                        EditorGUILayout.LabelField("  \"" + layer.Text + "\"", EditorStyles.miniLabel);
                    }
                    else if (layer.Sprite != null)
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(layer.Sprite, typeof(Sprite), false);
                        }

                        string path = AssetDatabase.GetAssetPath(layer.Sprite);

                        if (!string.IsNullOrEmpty(path))
                        {
                            EditorGUILayout.LabelField("  " + path, EditorStyles.miniLabel);
                        }
                    }

                    EditorGUILayout.LabelField(
                        "  rect " + layer.Rect.x.ToString("0") + "," + layer.Rect.y.ToString("0") +
                        "  " + layer.Rect.width.ToString("0") + "x" + layer.Rect.height.ToString("0"),
                        EditorStyles.miniLabel);

                    if (layer.IsText)
                    {
                        EditorGUILayout.LabelField(
                            "  size " + layer.FontSizeMin.ToString("0.0#") + " to " +
                            layer.FontSize.ToString("0.0#") +
                            (layer.Wrap ? ", wraps" : ", one line") + ", " + layer.Alignment,
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            "  fill " + layer.Fill +
                            (layer.Mask != null ? ", clipped to " + layer.Mask.name : ", unclipped"),
                            EditorStyles.miniLabel);
                    }

                    if (!string.IsNullOrEmpty(layer.LayerName))
                    {
                        EditorGUILayout.LabelField(
                            "  from '" + layer.LayerName + "' because " + layer.Reason,
                            EditorStyles.miniLabel);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        //  Drawing it, with the game's own painter
        // ------------------------------------------------------------------

        private void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(
                240f, 4000f, 320f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_factory == null)
            {
                return;
            }

            // Worked out on every pass, not only on a repaint: a handle has to
            // know where the card is in order to be dragged, and input events
            // arrive on their own passes.
            int width = Mathf.Max(64, Mathf.RoundToInt(area.height * (CardCanvas.Width / CardCanvas.Height)));
            int height = Mathf.Max(64, Mathf.RoundToInt(area.height));

            Rect texture = new Rect(area.x + (area.width - width) * 0.5f, area.y, width, height);

            if (_compare)
            {
                // Half the height each, so two whole cards fit whatever the
                // window has been dragged to.
                int half = Mathf.Max(64, Mathf.RoundToInt(area.height * 0.5f));
                int narrow = Mathf.Max(
                    64, Mathf.RoundToInt(half * (CardCanvas.Width / CardCanvas.Height)));

                float gap = 8f;
                float total = narrow * 2f + gap;
                float startX = area.x + (area.width - total) * 0.5f;

                RenderCard(
                    new Rect(startX, area.y, narrow, half), narrow, half, CardType.Minion);

                RenderCard(
                    new Rect(startX + narrow + gap, area.y, narrow, half),
                    narrow, half, CardType.Spell);

                return;
            }

            RenderCard(texture, width, height, _type);

            if (_editing)
            {
                DrawHandles(CardOnScreen(texture));
            }
        }

        /// <summary>
        /// Composes one card and draws it into a rectangle of the window.
        ///
        /// One card at a time, through the same factory and the same painter, so
        /// comparing a minion against a spell compares two runs of the identical
        /// code rather than two renderers.
        /// </summary>
        private void RenderCard(Rect into, int width, int height, CardType type)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStage();

            _factory.Compose(Describe(type), _plan);
            _painter.Apply(_plan);

            if (_target == null || _target.width != width || _target.height != height)
            {
                if (_target != null)
                {
                    _target.Release();
                    DestroyImmediate(_target);
                }

                _target = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave };
            }

            _camera.targetTexture = _target;
            _camera.Render();
            _camera.targetTexture = null;

            GUI.DrawTexture(into, _target, ScaleMode.ScaleToFit, false);
        }

        // ------------------------------------------------------------------
        //  Handles
        // ------------------------------------------------------------------

        /// <summary>Where the card itself sits inside the rendered texture.</summary>
        private static Rect CardOnScreen(Rect texture)
        {
            float width = texture.width * CardFraction;
            float height = texture.height * CardFraction;

            return new Rect(
                texture.x + (texture.width - width) * 0.5f,
                texture.y + (texture.height - height) * 0.5f,
                width,
                height);
        }

        private static Rect CanvasToScreen(Rect card, Rect canvas) =>
            new Rect(
                card.x + canvas.x / CardCanvas.Width * card.width,
                card.y + canvas.y / CardCanvas.Height * card.height,
                canvas.width / CardCanvas.Width * card.width,
                canvas.height / CardCanvas.Height * card.height);

        private static Vector2 ScreenToCanvas(Rect card, Vector2 screen) =>
            new Vector2(
                (screen.x - card.x) / card.width * CardCanvas.Width,
                (screen.y - card.y) / card.height * CardCanvas.Height);

        private float Snapped(float value) =>
            _snap <= 1 ? Mathf.Round(value) : Mathf.Round(value / _snap) * _snap;

        /// <summary>
        /// The eight corners and edges, then the body. Order matters: a corner
        /// sits inside the body, so it has to be offered the click first or the
        /// whole rectangle would move instead of resizing.
        /// </summary>
        private static readonly Vector2[] Grips =
        {
            new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0.5f),                      new Vector2(1f, 0.5f),
            new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f)
        };

        private const float GripSize = 9f;

        private void DrawHandles(Rect card)
        {
            CardVisualLayerDefinition layer = EditableLayer(out CardVisualRecipeAsset recipe);

            if (layer == null || recipe == null)
            {
                return;
            }

            Rect canvas = new Rect(layer.x, layer.y, layer.width, layer.height);
            Rect box = CanvasToScreen(card, canvas);

            if (Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();

                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Handles.DrawSolidRectangleWithOutline(
                    box, new Color(1f, 0.85f, 0.2f, 0.08f), new Color(1f, 0.85f, 0.2f, 0.9f));

                for (int index = 0; index < Grips.Length; index++)
                {
                    EditorGUI.DrawRect(GripAt(box, index), new Color(1f, 0.85f, 0.2f, 0.95f));
                }

                Handles.EndGUI();
            }

            // Cursors, so it is obvious what a handle will do before it is used.
            EditorGUIUtility.AddCursorRect(box, MouseCursor.MoveArrow);

            for (int index = 0; index < Grips.Length; index++)
            {
                EditorGUIUtility.AddCursorRect(GripAt(box, index), CursorFor(index));
            }

            HandleMouse(card, box, layer, recipe);
        }

        private static Rect GripAt(Rect box, int index)
        {
            Vector2 grip = Grips[index];

            return new Rect(
                box.x + box.width * grip.x - GripSize * 0.5f,
                box.y + box.height * grip.y - GripSize * 0.5f,
                GripSize,
                GripSize);
        }

        private static MouseCursor CursorFor(int index)
        {
            Vector2 grip = Grips[index];

            if (Mathf.Approximately(grip.x, 0.5f))
            {
                return MouseCursor.ResizeVertical;
            }

            if (Mathf.Approximately(grip.y, 0.5f))
            {
                return MouseCursor.ResizeHorizontal;
            }

            bool falling = Mathf.Approximately(grip.x, grip.y);
            return falling ? MouseCursor.ResizeUpLeft : MouseCursor.ResizeUpRight;
        }

        private void HandleMouse(
            Rect card, Rect box, CardVisualLayerDefinition layer, CardVisualRecipeAsset recipe)
        {
            Event current = Event.current;

            switch (current.type)
            {
                case EventType.MouseDown when current.button == 0:
                {
                    for (int index = 0; index < Grips.Length; index++)
                    {
                        if (GripAt(box, index).Contains(current.mousePosition))
                        {
                            Grab(index, current.mousePosition, layer);
                            current.Use();
                            return;
                        }
                    }

                    if (box.Contains(current.mousePosition))
                    {
                        Grab(Grips.Length, current.mousePosition, layer);
                        current.Use();
                    }

                    return;
                }

                case EventType.MouseDrag when _grabbed >= 0:
                {
                    Vector2 from = ScreenToCanvas(card, _grabbedAt);
                    Vector2 to = ScreenToCanvas(card, current.mousePosition);
                    Vector2 moved = to - from;

                    Undo.RecordObject(recipe, "Lay out " + _slot);
                    Apply(layer, moved);
                    EditorUtility.SetDirty(recipe);

                    current.Use();
                    Repaint();
                    return;
                }

                case EventType.MouseUp when _grabbed >= 0:
                {
                    _grabbed = -1;
                    current.Use();
                    return;
                }
            }
        }

        private void Grab(int index, Vector2 mouse, CardVisualLayerDefinition layer)
        {
            _grabbed = index;
            _grabbedAt = mouse;
            _grabbedRect = new Rect(layer.x, layer.y, layer.width, layer.height);

            GUI.FocusControl(null);
        }

        /// <summary>
        /// Moves or resizes from where the drag started, never from where the
        /// rectangle is now. Accumulating each frame's delta would drift, and a
        /// rectangle that ends up somewhere other than the cursor is worse than
        /// no handle at all.
        /// </summary>
        private void Apply(CardVisualLayerDefinition layer, Vector2 moved)
        {
            if (_grabbed == Grips.Length)
            {
                layer.x = Snapped(_grabbedRect.x + moved.x);
                layer.y = Snapped(_grabbedRect.y + moved.y);
                return;
            }

            Vector2 grip = Grips[_grabbed];

            float left = _grabbedRect.xMin;
            float top = _grabbedRect.yMin;
            float right = _grabbedRect.xMax;
            float bottom = _grabbedRect.yMax;

            if (Mathf.Approximately(grip.x, 0f))
            {
                left = Snapped(left + moved.x);
            }
            else if (Mathf.Approximately(grip.x, 1f))
            {
                right = Snapped(right + moved.x);
            }

            if (Mathf.Approximately(grip.y, 0f))
            {
                top = Snapped(top + moved.y);
            }
            else if (Mathf.Approximately(grip.y, 1f))
            {
                bottom = Snapped(bottom + moved.y);
            }

            // A rectangle dragged through itself keeps a minimum rather than
            // turning inside out.
            layer.x = Mathf.Min(left, right - 4f);
            layer.y = Mathf.Min(top, bottom - 4f);
            layer.width = Mathf.Max(4f, right - left);
            layer.height = Mathf.Max(4f, bottom - top);
        }

        /// <summary>
        /// A hidden object with the game's painter on it, and a camera pointed
        /// at it. Nothing about how a card is composed lives here; this is a
        /// place to stand and a lens to look through.
        /// </summary>
        /// <summary>
        /// Finds the real card prefab used by the game.
        ///
        /// The preview must not create a blank CardVisualPainter here: doing that
        /// loses all serialized painter settings from P_Card, including the
        /// per-role TMP fonts. Prefer the known project path, then fall back to an
        /// exact-name prefab search so moving the prefab does not silently break
        /// the preview.
        /// </summary>
        /// <summary>
        /// The card prefab this preview draws on.
        ///
        /// Asked of <see cref="CardPreviewCard"/> rather than looked up here, so
        /// that the preview and the capture tools cannot end up finding
        /// different cards - or, as happened once, one of them finding none and
        /// quietly drawing in the wrong font.
        /// </summary>
        private static GameObject LoadPreviewCardPrefab() => CardPreviewCard.Load();

        private void EnsureStage()
        {
            if (_stage != null)
            {
                return;
            }

            _stage = EditorUtility.CreateGameObjectWithHideFlags(
                "Card Visual Preview", HideFlags.HideAndDontSave);

            GameObject prefab = LoadPreviewCardPrefab();
            GameObject card;

            if (prefab != null)
            {
                card = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

                if (card == null)
                {
                    Debug.LogError(
                        "Card Visual Preview: P_Card was found but could not be instantiated.");
                    card = new GameObject("Card") { hideFlags = HideFlags.HideAndDontSave };
                }
                else
                {
                    card.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            else
            {
                Debug.LogError(
                    "Card Visual Preview: could not find P_Card.prefab. " +
                    "The preview is falling back to a blank painter, so serialized " +
                    "settings such as Title Font and Rules Font will be unavailable.");

                card = new GameObject("Card") { hideFlags = HideFlags.HideAndDontSave };
            }

            card.transform.SetParent(_stage.transform, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;

            _painter = card.GetComponent<CardVisualPainter>();

            if (_painter == null)
            {
                _painter = card.AddComponent<CardVisualPainter>();
                Debug.LogWarning(
                    "Card Visual Preview: the selected P_Card prefab has no CardVisualPainter; " +
                    "a blank fallback painter was added.");
            }

            GameObject eye = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            eye.transform.SetParent(_stage.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -3f);

            _camera = eye.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = CardCanvas.CardHeight * Framing;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            _camera.enabled = false;

            // Somewhere the scene is not, so a preview never renders the game.
            _stage.transform.position = new Vector3(10000f, 10000f, 10000f);
            _camera.cullingMask = ~0;
        }

        private void TearDownStage()
        {
            if (_target != null)
            {
                _target.Release();
                DestroyImmediate(_target);
                _target = null;
            }

            if (_stage != null)
            {
                DestroyImmediate(_stage);
                _stage = null;
                _painter = null;
                _camera = null;
            }
        }
    }
}
