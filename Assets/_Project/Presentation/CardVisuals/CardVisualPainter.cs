using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Draws a composed card, and is the only thing in the system that touches
    /// a GameObject.
    ///
    /// It owns a pool of renderers and reconfigures them. Nothing is
    /// instantiated when a card changes into a different kind of card, because
    /// there is nothing to instantiate: a spell is the same objects showing
    /// different pictures. That is the point of the whole exercise, and it is
    /// what makes one prefab enough for every card that will ever exist.
    ///
    /// It decides nothing. Which pictures, which order, which words and which
    /// rectangles all arrive already settled in the plan; this turns them into
    /// transforms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardVisualPainter : MonoBehaviour
    {
        [Tooltip("Material for the sprite layers. Left empty, a URP sprite material is made on demand.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip(
            "Font used by any role that has none of its own. Left empty too, TextMeshPro " +
            "uses its default.")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Fonts by role")]
        [Tooltip("The display face card names are set in.")]
        [SerializeField] private TMP_FontAsset titleFont;

        [Tooltip("The text face rules are set in.")]
        [SerializeField] private TMP_FontAsset rulesFont;

        [Tooltip("The face the mana, attack and health numbers are set in.")]
        [SerializeField] private TMP_FontAsset statFont;

        [Tooltip("The face a minion's tribe is set in.")]
        [SerializeField] private TMP_FontAsset tribeFont;

        [Tooltip("How much an unplayable card is dimmed. Zero is untouched, one is black.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimStrength = 0.55f;

        [Tooltip(
            "How much the writing on an unplayable card is dimmed. Less than the pictures, " +
            "because a card you cannot play is still a card you have to read in order to " +
            "decide what to do next turn.")]
        [Range(0f, 1f)]
        [SerializeField] private float textDimStrength = 0.28f;

        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private readonly List<TextMeshPro> _labels = new List<TextMeshPro>();
        private readonly List<Color> _spriteTints = new List<Color>();
        private readonly List<Color> _labelTints = new List<Color>();

        private Transform _layerRoot;
        private MaterialPropertyBlock _block;
        private Material _madeUpMaterial;
        private float _dim;

        /// <summary>
        /// The shader a card's layers are drawn with.
        ///
        /// Unity's built-in sprite shader is not a render pipeline shader, and
        /// under one it draws black — silently, and only when the pipeline is
        /// actually driving the camera, which is why a direct Camera.Render in
        /// a capture tool can look perfectly fine while the game does not.
        /// </summary>
        private const string LayerShader = "CoH/Card Layer";

        /// <summary>
        /// What to fall back to if the project's own shader is missing. Draws
        /// every layer correctly except that nothing can be clipped, which is
        /// visible but not broken.
        /// </summary>
        private const string PlainSpriteShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        private static readonly int MaskTexture = Shader.PropertyToID("_MaskTex");
        private static readonly int MaskScaleAndOffset = Shader.PropertyToID("_MaskST");

        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineColour = Shader.PropertyToID("_OutlineColor");

        /// <summary>
        /// The style each drawn label was set in, kept so that rewriting the
        /// words can bend them again.
        ///
        /// A curved title has to be re-warped every time it changes, because
        /// TextMeshPro rebuilds the mesh from scratch and throws the bent
        /// vertices away. Without this, a minion renamed mid-match would keep
        /// its curve until the card happened to be recomposed, and then lose it.
        /// </summary>
        private readonly List<CardTextStyle> _labelStyles = new List<CardTextStyle>();

        /// <summary>
        /// The width each label is meant to occupy, which is not the width it
        /// was laid out in. See <see cref="CardTextWarp"/>.
        /// </summary>
        private readonly List<float> _labelWidths = new List<float>();

        /// <summary>
        /// Which labels have been rebuilt by TextMeshPro and are waiting to be
        /// bent again.
        ///
        /// A note rather than the work itself, and that distinction is the
        /// whole of this. See <see cref="OnTextRegenerated"/>.
        /// </summary>
        private readonly List<bool> _labelNeedsWarp = new List<bool>();

        private bool _anyLabelNeedsWarp;

        /// <summary>How many sprite layers the last plan drew. Diagnostics and tests.</summary>
        public int SpriteLayerCount { get; private set; }

        /// <summary>How many text layers the last plan drew.</summary>
        public int TextLayerCount { get; private set; }

        /// <summary>
        /// How many renderers exist in the pool, drawn or not.
        ///
        /// A test watches this: recomposing a card as a different type must not
        /// grow it, because growing it would mean something was being created
        /// per variant after all.
        /// </summary>
        public int PooledRendererCount => _sprites.Count + _labels.Count;

        /// <summary>
        /// Listens for TextMeshPro rebuilding a mesh, so a bent title can be
        /// bent again.
        ///
        /// This is what the whole warp turned on, and what made the preview and
        /// the running game disagree about the same card. Bending a title edits
        /// the vertex buffer; almost anything afterwards — a colour change, a
        /// new string, a card being dimmed because it can no longer be played —
        /// marks the text dirty, and TextMeshPro then regenerates that buffer
        /// from the font and throws the curve away. In a still nothing came
        /// after the warp and it survived; in a match the card was dimmed a
        /// frame later and the title went flat, which is exactly how it looked
        /// on the table.
        /// </summary>
        private void OnEnable() =>
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextRegenerated);

        private void OnDisable() =>
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextRegenerated);

        /// <summary>
        /// Notes that a label needs bending again. Deliberately does not bend it.
        ///
        /// TextMeshPro raises this from inside the routine that builds the mesh,
        /// and goes on working afterwards: a warp applied here is overwritten
        /// before anything ever draws it. That was measured rather than guessed —
        /// the warp reported success on every rebuild and the mesh read flat
        /// immediately afterwards, every time. So the work is put off to
        /// <see cref="LateUpdate"/>, which runs when TextMeshPro has finished
        /// and where a warp is known to stick.
        /// </summary>
        private void OnTextRegenerated(Object changed)
        {
            TextMeshPro label = changed as TextMeshPro;

            if (label == null)
            {
                return;
            }

            int index = _labels.IndexOf(label);

            if (index < 0 || index >= _labelNeedsWarp.Count)
            {
                return;
            }

            _labelNeedsWarp[index] = true;
            _anyLabelNeedsWarp = true;
        }

        private void LateUpdate()
        {
            if (!_anyLabelNeedsWarp)
            {
                return;
            }

            _anyLabelNeedsWarp = false;

            for (int index = 0; index < _labelNeedsWarp.Count; index++)
            {
                if (!_labelNeedsWarp[index])
                {
                    continue;
                }

                _labelNeedsWarp[index] = false;

                TextMeshPro label = _labels[index];

                if (label == null || !label.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Not regenerating: the mesh TextMeshPro just built is the one
                // to bend, and asking for another would put us back inside the
                // routine this exists to stay out of.
                CardTextWarp.Apply(label, _labelStyles[index], _labelWidths[index], false);
            }
        }

        /// <summary>Draws a plan, reusing everything already built.</summary>
        public void Apply(CardVisualPlan plan)
        {
            EnsureRoot();

            int spriteIndex = 0;
            int labelIndex = 0;

            if (plan != null)
            {
                for (int index = 0; index < plan.Layers.Count; index++)
                {
                    CardVisualPlannedLayer layer = plan.Layers[index];

                    if (layer.IsText)
                    {
                        ApplyText(Label(labelIndex++), layer);
                    }
                    else
                    {
                        ApplySprite(Sprite(spriteIndex++), layer);
                    }
                }
            }

            SpriteLayerCount = spriteIndex;
            TextLayerCount = labelIndex;

            // Whatever the last card needed and this one does not.
            for (int index = spriteIndex; index < _sprites.Count; index++)
            {
                _sprites[index].gameObject.SetActive(false);
            }

            for (int index = labelIndex; index < _labels.Count; index++)
            {
                _labels[index].gameObject.SetActive(false);
            }

            Repaint();
        }

        /// <summary>
        /// Rewrites the words on an already drawn card, without composing it
        /// again.
        ///
        /// This is what a match spends its time doing. A minion buffed from 2/3
        /// to 4/5 is the same pictures in the same order with two different
        /// numbers, and re-resolving the catalog to discover that would be work
        /// done for nothing every time anything on the board changed.
        /// </summary>
        public void RefreshText(CardVisualPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            int labelIndex = 0;

            for (int index = 0; index < plan.Layers.Count && labelIndex < _labels.Count; index++)
            {
                if (!plan.Layers[index].IsText)
                {
                    continue;
                }

                TextMeshPro label = _labels[labelIndex];
                label.text = plan.Layers[index].Text;

                // New words mean a new mesh, and a new mesh is a straight one.
                CardTextWarp.Apply(
                    label, _labelStyles[labelIndex], _labelWidths[labelIndex], true);

                Settled(labelIndex);

                labelIndex++;
            }
        }

        /// <summary>
        /// Dims a card the engine will not let the player play.
        ///
        /// Applied over the composed colours rather than baked into them, so
        /// lighting a card back up is not a recomposition either.
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            float target = dimmed ? 1f : 0f;

            if (Mathf.Approximately(target, _dim))
            {
                return;
            }

            _dim = target;
            Repaint();
        }

        private void Repaint()
        {
            for (int index = 0; index < SpriteLayerCount && index < _sprites.Count; index++)
            {
                _sprites[index].color = Dimmed(_spriteTints[index], _dim * dimStrength);
            }

            for (int index = 0; index < TextLayerCount && index < _labels.Count; index++)
            {
                Color wanted = Dimmed(_labelTints[index], _dim * textDimStrength);

                // Only when it differs. Assigning a colour marks the text dirty
                // whatever it was, and every dirty mesh is one more rebuild for
                // the warp to survive.
                if (_labels[index].color != wanted)
                {
                    _labels[index].color = wanted;
                }
            }
        }

        private static Color Dimmed(Color colour, float amount)
        {
            if (amount <= 0f)
            {
                return colour;
            }

            Color dark = new Color(0.07f, 0.07f, 0.09f, colour.a);
            return Color.Lerp(colour, dark, amount);
        }

        /// <summary>
        /// The assigned material, or one made from the pipeline's own sprite
        /// shader.
        ///
        /// Made rather than required, so that a painter built in a test, a
        /// preview window or a capture tool draws the same as one from the
        /// prefab. Nothing that composes a card should have to remember to
        /// bring a material with it.
        /// </summary>
        private Material LayerMaterial()
        {
            if (spriteMaterial != null)
            {
                return spriteMaterial;
            }

            if (_madeUpMaterial == null)
            {
                Shader shader = Shader.Find(LayerShader) ?? Shader.Find(PlainSpriteShader);

                if (shader != null)
                {
                    _madeUpMaterial = new Material(shader) { name = "Card layer (generated)" };
                }
            }

            return _madeUpMaterial;
        }

        /// <summary>
        /// The face for a role, or the nearest thing assigned.
        ///
        /// The fallback is deliberate and ordered rather than absent: a project
        /// part way through acquiring its fonts should draw every card in
        /// whatever it does have, not lose its rules text because only a title
        /// face has been dropped in yet. Numbers fall back to the title face
        /// before the general one, because on a real card they are set in the
        /// display face; a tribe falls back the same way.
        /// </summary>
        private TMP_FontAsset FontFor(CardTextRole role)
        {
            switch (role)
            {
                case CardTextRole.Title:
                    return titleFont != null ? titleFont : font;

                case CardTextRole.Stat:
                    return statFont != null ? statFont : titleFont != null ? titleFont : font;

                case CardTextRole.Tribe:
                    return tribeFont != null ? tribeFont : titleFont != null ? titleFont : font;

                default:
                    return rulesFont != null ? rulesFont : font;
            }
        }

        /// <summary>Which faces are assigned. Reports and tests.</summary>
        public bool HasFontFor(CardTextRole role) => FontFor(role) != null;

        private void EnsureRoot()
        {
            if (_layerRoot != null)
            {
                return;
            }

            Transform existing = transform.Find("Layers");

            if (existing != null)
            {
                _layerRoot = existing;
                return;
            }

            GameObject root = new GameObject("Layers");
            root.transform.SetParent(transform, false);
            _layerRoot = root.transform;
        }

        private SpriteRenderer Sprite(int index)
        {
            while (_sprites.Count <= index)
            {
                GameObject layer = new GameObject("Sprite " + _sprites.Count);
                layer.transform.SetParent(_layerRoot, false);

                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Material material = LayerMaterial();

                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                _sprites.Add(renderer);
                _spriteTints.Add(Color.white);
            }

            _sprites[index].gameObject.SetActive(true);
            return _sprites[index];
        }

        private TextMeshPro Label(int index)
        {
            while (_labels.Count <= index)
            {
                GameObject layer = new GameObject("Text " + _labels.Count);
                layer.transform.SetParent(_layerRoot, false);

                TextMeshPro label = layer.AddComponent<TextMeshPro>();

                // A card is under a unit across and a name can be any length, so
                // the size on the layer is a ceiling rather than a size.
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.3f;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.Center;
                label.margin = Vector4.zero;
                label.raycastTarget = false;

                if (font != null)
                {
                    label.font = font;
                }

                label.GetComponent<Renderer>().shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                _labels.Add(label);
                _labelTints.Add(Color.white);
                _labelStyles.Add(CardTextStyle.For(CardVisualTextSlot.None));
                _labelWidths.Add(0f);
                _labelNeedsWarp.Add(false);
            }

            _labels[index].gameObject.SetActive(true);
            return _labels[index];
        }

        private void ApplySprite(SpriteRenderer renderer, in CardVisualPlannedLayer layer)
        {
            renderer.sprite = layer.Sprite;
            renderer.sortingOrder = layer.SortingOrder;

            Transform target = renderer.transform;
            target.localPosition = CardCanvas.ToLocalPosition(layer.Rect, layer.SortingOrder);
            target.localRotation = Quaternion.Euler(0f, 0f, -layer.Rotation);

            // A sprite draws at its own pixel size, so the scale that fills the
            // layer's rectangle depends on how big the imported image is. Which
            // means an artist can replace a 200 pixel gem with a 512 pixel one
            // and nothing moves.
            Vector2 wanted = CardCanvas.ToLocalSize(layer.Rect);
            Vector2 natural = NaturalSize(layer.Sprite);

            float scaleX = natural.x > 0f ? wanted.x / natural.x : 1f;
            float scaleY = natural.y > 0f ? wanted.y / natural.y : 1f;

            switch (layer.Fill)
            {
                case CardVisualFill.Cover:
                    // Up until it covers, keeping its proportions. A painting is
                    // whatever shape it was painted; squashing it to a window is
                    // never the right answer, so it overflows and is cropped.
                    scaleX = scaleY = Mathf.Max(scaleX, scaleY);
                    break;

                case CardVisualFill.Contain:
                    scaleX = scaleY = Mathf.Min(scaleX, scaleY);
                    break;
            }

            target.localScale = new Vector3(scaleX, scaleY, 1f);

            int index = _sprites.IndexOf(renderer);

            if (index >= 0)
            {
                _spriteTints[index] = layer.Tint;
            }

            ApplyMask(renderer, layer, wanted, natural, scaleX, scaleY);
        }

        /// <summary>
        /// Clips a layer to a shape, without touching the picture itself.
        ///
        /// The mask belongs to the rectangle rather than to the image, so a
        /// painting scaled up to cover its window has to be told where that
        /// window is in its own coordinates. That is the whole of the
        /// arithmetic below: how much bigger the drawn picture is than the
        /// rectangle, and therefore how far into the picture the window sits.
        /// </summary>
        private void ApplyMask(
            SpriteRenderer renderer,
            in CardVisualPlannedLayer layer,
            Vector2 wanted,
            Vector2 natural,
            float scaleX,
            float scaleY)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);

            if (layer.Mask == null || layer.Mask.texture == null)
            {
                _block.SetTexture(MaskTexture, Texture2D.whiteTexture);
                _block.SetVector(MaskScaleAndOffset, new Vector4(1f, 1f, 0f, 0f));
            }
            else
            {
                float drawnWidth = natural.x * scaleX;
                float drawnHeight = natural.y * scaleY;

                float ratioX = wanted.x > 0f ? drawnWidth / wanted.x : 1f;
                float ratioY = wanted.y > 0f ? drawnHeight / wanted.y : 1f;

                _block.SetTexture(MaskTexture, layer.Mask.texture);
                _block.SetVector(MaskScaleAndOffset, new Vector4(
                    ratioX, ratioY, 0.5f - 0.5f * ratioX, 0.5f - 0.5f * ratioY));
            }

            renderer.SetPropertyBlock(_block);
        }

        private static TextAlignmentOptions Alignment(CardVisualAlignment alignment)
        {
            switch (alignment)
            {
                case CardVisualAlignment.Top: return TextAlignmentOptions.Top;
                case CardVisualAlignment.Bottom: return TextAlignmentOptions.Bottom;
                case CardVisualAlignment.Left: return TextAlignmentOptions.Left;
                case CardVisualAlignment.Right: return TextAlignmentOptions.Right;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static Vector2 NaturalSize(Sprite sprite)
        {
            if (sprite == null || sprite.pixelsPerUnit <= 0f)
            {
                return Vector2.one;
            }

            return new Vector2(
                sprite.rect.width / sprite.pixelsPerUnit,
                sprite.rect.height / sprite.pixelsPerUnit);
        }

        private void ApplyText(TextMeshPro label, in CardVisualPlannedLayer layer)
        {
            // Before the text, because changing the face rebuilds the mesh and
            // the material with it, which would undo an outline set first.
            TMP_FontAsset face = FontFor(layer.TextStyle.Role);

            if (face != null && label.font != face)
            {
                label.font = face;
            }

            label.text = layer.Text;
            label.fontSizeMax = layer.FontSize;

            // A floor as well as a ceiling. Without one a long name shrinks
            // until it is unreadable rather than admitting it does not fit, and
            // the card silently becomes worse the more you write on it.
            label.fontSizeMin = Mathf.Min(layer.FontSizeMin, layer.FontSize);

            label.fontStyle = layer.Bold ? FontStyles.Bold : FontStyles.Normal;
            label.textWrappingMode = layer.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.alignment = Alignment(layer.Alignment);

            Vector2 slot = CardCanvas.ToLocalSize(layer.Rect);

            // A label that may be squeezed is laid out in a wider box than it
            // will occupy, so that its height decides its size and the squeeze
            // brings the width back. Without this a long name is shrunk to fit
            // across, and a short name in a large banner is sized by nothing at
            // all — which is how the titles ended up looking like interface
            // text rather than titles.
            float layoutWidth = layer.TextStyle.CanCondense
                ? slot.x / layer.TextStyle.MinCondense
                : slot.x;

            label.rectTransform.sizeDelta = new Vector2(layoutWidth, slot.y);
            label.sortingOrder = layer.SortingOrder;

            Transform target = label.transform;
            target.localPosition = CardCanvas.ToLocalPosition(layer.Rect, layer.SortingOrder);
            target.localRotation = Quaternion.Euler(0f, 0f, -layer.Rotation);
            target.localScale = Vector3.one;

            label.characterSpacing = layer.TextStyle.Tracking;

            if (layer.TextStyle.LineSpacing != 0f)
            {
                label.lineSpacing = layer.TextStyle.LineSpacing;
            }

            ApplyOutline(label, layer.TextStyle);

            int index = _labels.IndexOf(label);

            if (index >= 0)
            {
                _labelTints[index] = layer.Tint;
                _labelStyles[index] = layer.TextStyle;
                _labelWidths[index] = slot.x;
            }

            CardTextWarp.Apply(label, layer.TextStyle, slot.x, true);

            // Bent here and now, so nothing is owed.
            //
            // Bending regenerates the mesh first, and regenerating it is exactly
            // what asks for a bend later: without this the label is bent twice,
            // once now and once on the next late update, and the second one
            // works on a mesh that is already curved. On the table that went
            // unnoticed, because dimming a card recolours its text a frame later
            // and the clean rebuild that follows leaves a single bend. On a bare
            // painter — which is what the preview and every capture tool use —
            // nothing ever recolours it, so the doubled curve stayed, and a
            // still showed a title arched half again as far as the game drew it.
            Settled(index);
        }

        /// <summary>Notes that a label is bent and needs nothing further.</summary>
        private void Settled(int index)
        {
            if (index >= 0 && index < _labelNeedsWarp.Count)
            {
                _labelNeedsWarp[index] = false;
            }
        }

        /// <summary>
        /// Draws the label's outline, the way a card's numbers and titles carry
        /// one.
        ///
        /// Through the label's own material instance rather than a property
        /// block, because TextMeshPro reads the outline in its shader and a
        /// block would be overwritten the next time it rebuilt the mesh. The
        /// padding has to be recomputed too: an outline is drawn outside the
        /// glyph, and without room for it the thick stroke on a title is sliced
        /// off square at the edge of the character.
        /// </summary>
        private static void ApplyOutline(TextMeshPro label, in CardTextStyle style)
        {
            Material material = label.fontMaterial;

            if (material == null || !material.HasProperty(OutlineWidth))
            {
                return;
            }

            material.SetFloat(OutlineWidth, style.OutlineWidth);

            if (material.HasProperty(OutlineColour))
            {
                material.SetColor(OutlineColour, style.OutlineColor);
            }

            label.UpdateMeshPadding();
        }
    }
}
