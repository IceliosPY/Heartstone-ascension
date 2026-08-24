using CoH.Core.Identifiers;
using TMPro;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// One minion on the board.
    ///
    /// Tied to the engine by <see cref="EntityId"/> and nothing else, so a view
    /// and the minion it stands for can never drift apart: when a death event
    /// names an id, exactly one view answers to it.
    /// </summary>
    public sealed class MinionView : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;

        [Header("Feedback")]
        [SerializeField] private Color baseColor = new Color(0.32f, 0.42f, 0.30f, 1f);
        [SerializeField] private Color readyColor = new Color(0.42f, 0.62f, 0.36f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color targetableColor = new Color(0.85f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color damagedHealthColor = new Color(0.95f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color healthyHealthColor = Color.white;

        private MaterialPropertyBlock _block;
        private bool _canAttack;
        private bool _isSelected;
        private bool _isTargetable;

        public EntityId EntityId { get; private set; }

        public bool CanAttack => _canAttack;

        public void Bind(MinionViewModel model)
        {
            EntityId = model.EntityId;
            _canAttack = model.CanAttack;

            if (nameText != null)
            {
                nameText.text = model.DisplayName;
            }

            if (attackText != null)
            {
                attackText.text = model.Attack.ToString();
            }

            if (healthText != null)
            {
                healthText.text = model.CurrentHealth.ToString();
                healthText.color = model.IsDamaged ? damagedHealthColor : healthyHealthColor;
            }

            ApplyTint();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
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

            Color tint = baseColor;

            if (_canAttack)
            {
                tint = readyColor;
            }

            if (_isTargetable)
            {
                tint = targetableColor;
            }

            if (_isSelected)
            {
                tint = selectedColor;
            }

            _block ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(_block);
            _block.SetColor(ShaderIds.BaseColor, tint);
            body.SetPropertyBlock(_block);
        }
    }
}
