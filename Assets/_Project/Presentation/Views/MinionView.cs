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
    ///
    /// The attack and health numbers sit on their own plates, tucked inside the
    /// minion's own footprint. That is not decoration: with seven minions in a
    /// row, loose numbers floating at the edges of each one run into their
    /// neighbours and stop being readable at all.
    /// </summary>
    public sealed class MinionView : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private Renderer body;
        [SerializeField] private Renderer attackPlate;
        [SerializeField] private Renderer healthPlate;
        [SerializeField] private GameObject selectionRing;
        [SerializeField] private GameObject targetRing;

        [Header("Text")]
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro attackText;
        [SerializeField] private TextMeshPro healthText;

        [Header("Palette")]
        [SerializeField] private Color restingColor = new Color(0.34f, 0.42f, 0.32f);
        [SerializeField] private Color readyColor = new Color(0.44f, 0.68f, 0.38f);
        [SerializeField] private Color attackPlateColor = new Color(0.82f, 0.64f, 0.16f);
        [SerializeField] private Color healthPlateColor = new Color(0.74f, 0.18f, 0.18f);
        [SerializeField] private Color hurtPlateColor = new Color(0.95f, 0.32f, 0.28f);

        private MaterialPropertyBlock _block;
        private bool _canAttack;

        public EntityId EntityId { get; private set; }

        /// <summary>Whether the engine says this minion has something to attack.</summary>
        public bool CanAttack => _canAttack;

        public void Bind(MinionViewModel model)
        {
            EntityId = model.EntityId;
            _canAttack = model.CanAttack;

            SetText(nameText, model.DisplayName);
            SetText(attackText, model.Attack.ToString());
            SetText(healthText, model.CurrentHealth.ToString());

            Tint(body, _canAttack ? readyColor : restingColor);
            Tint(attackPlate, attackPlateColor);
            Tint(healthPlate, model.IsDamaged ? hurtPlateColor : healthPlateColor);
        }

        public void SetSelected(bool selected)
        {
            if (selectionRing != null)
            {
                selectionRing.SetActive(selected);
            }
        }

        public void SetTargetable(bool targetable)
        {
            if (targetRing != null)
            {
                targetRing.SetActive(targetable);
            }
        }

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

        private static void SetText(TextMeshPro target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
