using CoH.Core.Identifiers;
using TMPro;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// One card in a hand.
    ///
    /// Built as a stack of separate pieces rather than one picture, and that is
    /// the whole point of the prefab: every slot below is a placeholder quad
    /// today and a painted sprite later, with no code change in between. A cost
    /// going from 5 to 3 rewrites a label; it never regenerates an image.
    ///
    /// The proportions come from measuring how HearthCards composes a card: an
    /// 800 by 1100 canvas with the mana gem top left, the name banner across
    /// the middle, the rules parchment below it, and the attack and health gems
    /// in the bottom corners. Their artwork is not used, only the geometry that
    /// makes a card read as a Hearthstone card.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Renderer frame;
        [SerializeField] private Renderer artwork;
        [SerializeField] private Renderer manaGem;
        [SerializeField] private Renderer rarityGem;
        [SerializeField] private GameObject tribeBanner;
        [SerializeField] private GameObject statistics;
        [SerializeField] private GameObject faceDownCover;

        [Header("Text")]
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro manaText;
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro rulesText;
        [SerializeField] private TextMeshPro tribeText;

        [Header("Palette")]
        [SerializeField] private Color frameColor = new Color(0.55f, 0.38f, 0.21f);
        [SerializeField] private Color artworkColor = new Color(0.26f, 0.33f, 0.42f);
        [SerializeField] private Color manaColor = new Color(0.16f, 0.42f, 0.85f);
        [SerializeField] private Color selectedFrameColor = new Color(1f, 0.84f, 0.38f);

        [Tooltip("How much an unplayable card is dimmed. Zero is untouched, one is black.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimStrength = 0.55f;

        [Header("Hover")]
        [Tooltip("How far a hovered card rises out of the hand.")]
        [SerializeField] private float hoverLift = 0.5f;

        [Tooltip("How far it comes toward the camera, which is what puts it in front of its neighbours.")]
        [SerializeField] private float hoverForward = 0.62f;

        [SerializeField] private float hoverScale = 1.24f;

        [Tooltip("How quickly a card reaches its target pose. Higher is snappier.")]
        [SerializeField] private float poseSmoothing = 18f;

        private MaterialPropertyBlock _block;
        private bool _isHovered;
        private bool _isPlayable;
        private bool _isFaceDown;
        private bool _isDragging;

        private Vector3 _restingPosition;
        private Quaternion _restingRotation = Quaternion.identity;
        private float _restingScale = 1f;
        private bool _hasPose;
        private Transform _poseParent;

        private Collider _collider;

        /// <summary>Which card instance in the engine this view stands for.</summary>
        public EntityId EntityId { get; private set; }

        /// <summary>Whether the engine says this card can be played right now.</summary>
        public bool IsPlayable => _isPlayable;

        public bool IsFaceDown => _isFaceDown;

        /// <summary>True while the pointer is over this card and it has risen.</summary>
        public bool IsHovered => _isHovered;

        /// <summary>True while this card is following the pointer.</summary>
        public bool IsDragging => _isDragging;

        /// <summary>Where the layout wants this card, whatever it is doing right now.</summary>
        public Vector3 RestingLocalPosition => _restingPosition;

        private void Awake() => _collider = GetComponent<Collider>();

        /// <summary>
        /// Records where the layout wants this card.
        ///
        /// Hovering and dragging are offsets from this pose, never edits to it,
        /// which is what stops a card drifting a little further out of the hand
        /// every time the pointer crosses it. However an interaction ends, the
        /// card returns to exactly what the fan computed.
        /// </summary>
        public void SetRestingPose(Vector3 localPosition, Quaternion localRotation, float scale)
        {
            _restingPosition = localPosition;
            _restingRotation = localRotation;
            _restingScale = scale;

            // A card eases to a new pose when the hand re-fans under it, and
            // arrives instantly when it is new or when the board has just
            // flipped. Easing across a turn change would mean sliding somebody
            // else's hand across the table.
            bool snap = !_hasPose || _poseParent != transform.parent;

            _hasPose = true;
            _poseParent = transform.parent;

            if (snap && !_isDragging)
            {
                ApplyPose(1f);
            }
        }

        /// <summary>Raises the card so it can be read, and brings it in front of its neighbours.</summary>
        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered)
            {
                return;
            }

            _isHovered = hovered;
            Repaint();
        }

        /// <summary>
        /// Takes the card out of the hand so it can follow the pointer. The
        /// collider goes with it: a card under the cursor would otherwise be the
        /// first thing every ray meets, and the board could never be aimed at.
        /// </summary>
        public void BeginDrag(Transform dragLayer)
        {
            _isDragging = true;
            _isHovered = false;

            if (dragLayer != null)
            {
                transform.SetParent(dragLayer, true);
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            Repaint();
        }

        /// <summary>Places the dragged card, in world space, under the pointer.</summary>
        public void UpdateDrag(Vector3 worldPosition, Quaternion worldRotation, float scale)
        {
            if (!_isDragging)
            {
                return;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// Puts the card back under the hand without moving it, so it glides
        /// home from wherever it was let go rather than blinking there.
        /// </summary>
        public void EndDrag(Transform handAnchor)
        {
            _isDragging = false;

            if (handAnchor != null)
            {
                transform.SetParent(handAnchor, true);
                _poseParent = handAnchor;
            }

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            Repaint();
        }

        private void LateUpdate()
        {
            if (_isDragging || !_hasPose)
            {
                return;
            }

            // Frame rate independent easing: what stays constant is the fraction
            // of the remaining distance covered per second, not per frame.
            ApplyPose(1f - Mathf.Exp(-poseSmoothing * Time.deltaTime));
        }

        private void ApplyPose(float t)
        {
            Vector3 targetPosition = _isHovered
                ? _restingPosition + new Vector3(0f, hoverLift, -hoverForward)
                : _restingPosition;

            // A hovered card straightens up out of the fan, which is most of
            // what makes it readable.
            Quaternion targetRotation = _isHovered ? Quaternion.identity : _restingRotation;
            float targetScale = _isHovered ? _restingScale * hoverScale : _restingScale;

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, t);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, t);
        }

        /// <summary>Shows a card, face up, from a snapshot the presenter built.</summary>
        public void Bind(CardViewModel model)
        {
            EntityId = model.EntityId;
            _isPlayable = model.IsPlayable;

            SetFaceDown(false);

            SetText(nameText, model.DisplayName);
            SetText(manaText, model.ManaCost.ToString());
            SetText(rulesText, model.RulesText);

            if (statistics != null)
            {
                statistics.SetActive(model.ShowsStatistics);
            }

            SetText(attackText, model.Attack.ToString());
            SetText(healthText, model.Health.ToString());

            bool hasTribe = model.Tribe != Core.Cards.Tribe.None;

            if (tribeBanner != null)
            {
                tribeBanner.SetActive(hasTribe);
            }

            SetText(tribeText, hasTribe ? model.Tribe.ToString().ToUpperInvariant() : string.Empty);

            Repaint();
        }

        /// <summary>
        /// Shows the back of a card. Used for the waiting player's hand, where
        /// the count matters and the contents do not.
        /// </summary>
        public void BindFaceDown()
        {
            EntityId = EntityId.None;
            _isPlayable = false;
            _isHovered = false;
            SetFaceDown(true);
        }

        private void SetFaceDown(bool faceDown)
        {
            _isFaceDown = faceDown;

            if (faceDownCover != null)
            {
                faceDownCover.SetActive(faceDown);
            }

            SetVisible(artwork, !faceDown);
            SetVisible(rarityGem, !faceDown);
            SetVisible(manaGem, !faceDown);
            SetActive(nameText, !faceDown);
            SetActive(manaText, !faceDown);
            SetActive(rulesText, !faceDown);

            if (faceDown)
            {
                if (statistics != null)
                {
                    statistics.SetActive(false);
                }

                if (tribeBanner != null)
                {
                    tribeBanner.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Paints the card for its current state.
        ///
        /// An unplayable card is dimmed rather than merely refused on click, so
        /// a player can tell at a glance what they can afford. The judgement
        /// itself is never made here: it arrives already decided in the model.
        /// </summary>
        private void Repaint()
        {
            if (_isFaceDown)
            {
                return;
            }

            // A card being read is never dimmed. Not affording a card stops it
            // being played, not inspected, and a player deciding what to do next
            // needs to read exactly the ones they cannot afford yet.
            bool lit = _isPlayable || _isHovered || _isDragging;
            float dim = lit ? 0f : dimStrength;

            Tint(frame, _isDragging || _isHovered ? selectedFrameColor : Dimmed(frameColor, dim));
            Tint(artwork, Dimmed(artworkColor, dim));
            Tint(manaGem, Dimmed(manaColor, dim));

            Color textTint = lit ? Color.white : new Color(0.62f, 0.62f, 0.66f);

            Fade(nameText, textTint);
            Fade(manaText, textTint);
            Fade(attackText, textTint);
            Fade(healthText, textTint);
        }

        private static Color Dimmed(Color colour, float amount) =>
            Color.Lerp(colour, new Color(0.07f, 0.07f, 0.09f), amount);

        private void Tint(Renderer target, Color colour)
        {
            if (target == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, colour);
            target.SetPropertyBlock(_block);
        }

        private static void Fade(TextMeshPro target, Color colour)
        {
            if (target != null)
            {
                target.color = colour;
            }
        }

        private static void SetText(TextMeshPro target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetVisible(Renderer target, bool visible)
        {
            if (target != null)
            {
                target.enabled = visible;
            }
        }
    }

    /// <summary>Shader property ids, resolved once.</summary>
    internal static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    }
}
