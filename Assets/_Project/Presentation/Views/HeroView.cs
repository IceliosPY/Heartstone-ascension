using CoH.Core.Identifiers;
using CoH.Core.State;
using TMPro;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// One player's hero: a portrait plate, their name, their health, their
    /// armour when they have any, and the counters that used to clutter the HUD.
    ///
    /// Bound by side rather than owned by a seat. In hotseat the near hero is
    /// whoever is acting, so the same view object shows player one on one turn
    /// and player two on the next; <see cref="PlayerId"/> and
    /// <see cref="EntityId"/> come from whatever it was last given.
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private Renderer plate;
        [SerializeField] private Renderer portrait;
        [SerializeField] private Renderer healthPlate;
        [SerializeField] private Renderer armorPlate;
        [SerializeField] private GameObject armorBadge;
        [SerializeField] private GameObject targetRing;

        [Header("Text")]
        [SerializeField] private TextMeshPro nameText;
        [SerializeField] private TextMeshPro healthText;
        [SerializeField] private TextMeshPro armorText;
        [SerializeField] private TextMeshPro countersText;

        [Header("Palette")]
        [SerializeField] private Color nearColor = new Color(0.24f, 0.32f, 0.52f);
        [SerializeField] private Color farColor = new Color(0.50f, 0.27f, 0.27f);
        [SerializeField] private Color portraitColor = new Color(0.30f, 0.28f, 0.34f);
        [SerializeField] private Color healthColor = new Color(0.74f, 0.18f, 0.18f);
        [SerializeField] private Color armorColor = new Color(0.36f, 0.55f, 0.80f);

        [Tooltip("Ring colour for a target that is legal but not being pointed at.")]
        [SerializeField] private Color targetRestingColor = new Color(0.85f, 0.25f, 0.22f, 1f);

        [Tooltip("Ring colour for the target under the pointer.")]
        [SerializeField] private Color targetHoveredColor = new Color(1f, 0.85f, 0.35f, 1f);

        private MaterialPropertyBlock _block;
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

        public PlayerId PlayerId { get; private set; }

        /// <param name="isNear">True when this is the acting player's hero.</param>
        public void Bind(Player player, string label, bool isNear)
        {
            Hero hero = player.Hero;

            EntityId = hero.Id;
            PlayerId = player.Id;

            SetText(nameText, label);
            SetText(healthText, hero.CurrentHealth.ToString());
            SetText(countersText, "deck " + player.Deck.Count + "   hand " + player.Hand.Count);

            bool hasArmor = hero.Armor > 0;

            if (armorBadge != null)
            {
                armorBadge.SetActive(hasArmor);
            }

            SetText(armorText, hero.Armor.ToString());

            Tint(plate, isNear ? nearColor : farColor);
            Tint(portrait, portraitColor);
            Tint(healthPlate, healthColor);
            Tint(armorPlate, armorColor);
        }

        /// <summary>
        /// Marks this hero as something the current attacker may hit. A hero is
        /// a target exactly when the engine lists it, and no rule about heroes
        /// lives here.
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

        /// <summary>Strengthens the marker while the pointer is over this hero.</summary>
        public void SetTargetHighlighted(bool highlighted)
        {
            if (_targetRingRenderer == null)
            {
                return;
            }

            Tint(_targetRingRenderer, highlighted ? targetHoveredColor : targetRestingColor);
            _targetRingRenderer.transform.localScale = highlighted
                ? _targetRingScale * 1.12f
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
