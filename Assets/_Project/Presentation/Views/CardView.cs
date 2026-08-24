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

        [Header("Feedback")]
        [SerializeField] private Color playableTint = Color.white;
        [SerializeField] private Color unplayableTint = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] private Color selectedTint = new Color(1f, 0.92f, 0.45f, 1f);

        private MaterialPropertyBlock _block;
        private bool _isSelected;
        private bool _isPlayable;

        /// <summary>Which card instance in the engine this view stands for.</summary>
        public EntityId EntityId { get; private set; }

        public bool IsPlayable => _isPlayable;

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

            ApplyTint();
        }

        /// <summary>
        /// Shows the back of a card. Used for the opponent's hand, where the
        /// count matters and the contents do not.
        /// </summary>
        public void BindFaceDown()
        {
            EntityId = EntityId.None;
            _isPlayable = false;
            _isSelected = false;
            SetFaceDown(true);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyTint();
        }

        private void SetFaceDown(bool faceDown)
        {
            if (faceDownCover != null)
            {
                faceDownCover.SetActive(faceDown);
            }

            SetVisible(artwork, !faceDown);
            SetVisible(rarityGem, !faceDown);
            SetActive(nameText, !faceDown);
            SetActive(manaText, !faceDown);
            SetActive(rulesText, !faceDown);

            if (statistics != null && faceDown)
            {
                statistics.SetActive(false);
            }

            if (tribeBanner != null && faceDown)
            {
                tribeBanner.SetActive(false);
            }
        }

        private void ApplyTint()
        {
            if (frame == null)
            {
                return;
            }

            Color tint = _isSelected ? selectedTint : (_isPlayable ? playableTint : unplayableTint);

            _block ??= new MaterialPropertyBlock();
            frame.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, tint * frameBaseColor);
            frame.SetPropertyBlock(_block);
        }

        [SerializeField] private Color frameBaseColor = new Color(0.52f, 0.36f, 0.20f, 1f);

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
