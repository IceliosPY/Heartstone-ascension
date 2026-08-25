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

        [Tooltip("Ring colour for a target that is legal but not being pointed at.")]
        [SerializeField] private Color targetRestingColor = new Color(0.85f, 0.25f, 0.22f, 1f);

        [Tooltip("Ring colour for the target under the pointer.")]
        [SerializeField] private Color targetHoveredColor = new Color(1f, 0.85f, 0.35f, 1f);

        private MaterialPropertyBlock _block;
        private bool _canAttack;
        private Renderer _targetRingRenderer;
        private Vector3 _targetRingScale = Vector3.one;

        private void Awake()
        {
            if (targetRing != null)
            {
                _targetRingRenderer = targetRing.GetComponent<Renderer>();
                _targetRingScale = targetRing.transform.localScale;
            }
        }

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

        /// <summary>
        /// Marks this minion as something the current attacker may hit. The list
        /// this comes from is the engine's; the view only paints it.
        /// </summary>
        public void SetTargetable(bool targetable)
        {
            if (targetRing != null)
            {
                targetRing.SetActive(targetable);
            }

            if (targetable)
            {
                SetTargetHighlighted(false);
            }
        }

        /// <summary>
        /// Strengthens the marker on the one legal target the pointer is
        /// actually over, so a player aiming across a crowded board can tell
        /// which of the highlighted minions they are about to hit.
        /// </summary>
        public void SetTargetHighlighted(bool highlighted)
        {
            if (_targetRingRenderer == null)
            {
                return;
            }

            Tint(_targetRingRenderer, highlighted ? targetHoveredColor : targetRestingColor);
            _targetRingRenderer.transform.localScale = highlighted
                ? _targetRingScale * 1.18f
                : _targetRingScale;
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
