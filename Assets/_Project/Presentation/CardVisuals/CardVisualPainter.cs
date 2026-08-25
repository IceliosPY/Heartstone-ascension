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

        [Tooltip("Font for the text layers. Left empty, TextMeshPro uses its default.")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("How much an unplayable card is dimmed. Zero is untouched, one is black.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimStrength = 0.55f;

        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private readonly List<TextMeshPro> _labels = new List<TextMeshPro>();
        private readonly List<Color> _spriteTints = new List<Color>();
        private readonly List<Color> _labelTints = new List<Color>();

        private Transform _layerRoot;
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
        private const string LayerShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

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
                if (plan.Layers[index].IsText)
                {
                    _labels[labelIndex++].text = plan.Layers[index].Text;
                }
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
            float target = dimmed ? dimStrength : 0f;

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
                _sprites[index].color = Dimmed(_spriteTints[index], _dim);
            }

            for (int index = 0; index < TextLayerCount && index < _labels.Count; index++)
            {
                _labels[index].color = Dimmed(_labelTints[index], _dim);
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
                Shader shader = Shader.Find(LayerShader);

                if (shader != null)
                {
                    _madeUpMaterial = new Material(shader) { name = "Card layer (generated)" };
                }
            }

            return _madeUpMaterial;
        }

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

            target.localScale = new Vector3(
                natural.x > 0f ? wanted.x / natural.x : 1f,
                natural.y > 0f ? wanted.y / natural.y : 1f,
                1f);

            int index = _sprites.IndexOf(renderer);

            if (index >= 0)
            {
                _spriteTints[index] = layer.Tint;
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
            label.text = layer.Text;
            label.fontSizeMax = layer.FontSize;
            label.fontStyle = layer.Bold ? FontStyles.Bold : FontStyles.Normal;
            label.rectTransform.sizeDelta = CardCanvas.ToLocalSize(layer.Rect);
            label.sortingOrder = layer.SortingOrder;

            Transform target = label.transform;
            target.localPosition = CardCanvas.ToLocalPosition(layer.Rect, layer.SortingOrder);
            target.localRotation = Quaternion.Euler(0f, 0f, -layer.Rotation);
            target.localScale = Vector3.one;

            int index = _labels.IndexOf(label);

            if (index >= 0)
            {
                _labelTints[index] = layer.Tint;
            }
        }
    }
}
