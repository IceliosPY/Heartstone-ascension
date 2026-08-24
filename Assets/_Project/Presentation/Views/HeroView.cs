using CoH.Core.Identifiers;
using CoH.Core.State;
using TMPro;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// One player's hero: health, armour when there is any, and a collider so it
    /// can be attacked like anything else on the board.
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro armorText;
        [SerializeField] private GameObject armorBadge;

        [Header("Feedback")]
        [SerializeField] private Color baseColor = new Color(0.28f, 0.30f, 0.44f, 1f);
        [SerializeField] private Color targetableColor = new Color(0.85f, 0.35f, 0.35f, 1f);

        private MaterialPropertyBlock _block;
        private bool _isTargetable;

        public EntityId EntityId { get; private set; }

        public PlayerId PlayerId { get; private set; }

        public void Bind(Hero hero, string label)
        {
            EntityId = hero.Id;
            PlayerId = hero.Owner;

            if (nameText != null)
            {
                nameText.text = label;
            }

            if (healthText != null)
            {
                healthText.text = hero.CurrentHealth.ToString();
            }

            bool hasArmor = hero.Armor > 0;

            if (armorBadge != null)
            {
                armorBadge.SetActive(hasArmor);
            }

            if (armorText != null)
            {
                armorText.text = hero.Armor.ToString();
            }

            ApplyTint();
        }

        public void SetTargetable(bool targetable)
        {
            _isTargetable = targetable;
            ApplyTint();
        }

        private void ApplyTint()
        {
            if (body == null)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, _isTargetable ? targetableColor : baseColor);
            body.SetPropertyBlock(_block);
        }
    }
}
