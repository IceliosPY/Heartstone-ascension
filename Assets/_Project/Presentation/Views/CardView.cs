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

        private MaterialPropertyBlock _block;
        private bool _isSelected;
        private bool _isPlayable;
        private bool _isFaceDown;
        private Vector3 _restingPosition;
        private float _selectionLift;

        /// <summary>Which card instance in the engine this view stands for.</summary>
        public EntityId EntityId { get; private set; }

        /// <summary>Whether the engine says this card can be played right now.</summary>
        public bool IsPlayable => _isPlayable;

        public bool IsFaceDown => _isFaceDown;

        /// <summary>
        /// Records where the layout wants this card, so a selected card can lift
        /// out of the hand and drop back without the layout being consulted.
        /// </summary>
        public void SetRestingPose(Vector3 localPosition, Quaternion localRotation, float scale, float selectionLift)
        {
            _restingPosition = localPosition;
            _selectionLift = selectionLift;

            transform.localRotation = localRotation;
            transform.localScale = Vector3.one * scale;

            ApplyLift();
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
            _isSelected = false;
            SetFaceDown(true);
            ApplyLift();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyLift();
            Repaint();
        }

        private void ApplyLift()
        {
            transform.localPosition = _isSelected
                ? _restingPosition + Vector3.up * _selectionLift
                : _restingPosition;
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

            float dim = _isPlayable || _isSelected ? 0f : dimStrength;

            Tint(frame, _isSelected ? selectedFrameColor : Dimmed(frameColor, dim));
            Tint(artwork, Dimmed(artworkColor, dim));
            Tint(manaGem, Dimmed(manaColor, dim));

            Color textTint = _isPlayable || _isSelected ? Color.white : new Color(0.62f, 0.62f, 0.66f);

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
