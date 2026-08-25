using System.Collections;
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
    public sealed class HeroView : MonoBehaviour, ICombatTargetView
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

        [Header("Feedback")]
        [SerializeField] private Transform impactAnchor;

        [SerializeField] private Color hitFlashColor = new Color(1f, 0.72f, 0.62f);

        [SerializeField] private float hitRecoil = 0.13f;

        private MaterialPropertyBlock _block;
        private Renderer _targetRingRenderer;
        private Vector3 _targetRingScale = Vector3.one;

        private Color _plateColor;
        private Vector3 _restingLocal;
        private bool _hasResting;
        private Coroutine _feedback;

        private void Awake()
        {
            if (targetRing != null)
            {
                _targetRingRenderer = targetRing.GetComponent<Renderer>();
                _targetRingScale = targetRing.transform.localScale;
            }

            _restingLocal = transform.localPosition;
            _hasResting = true;
        }

        /// <summary>Where an attack should land on this hero.</summary>
        public Vector3 ImpactPoint =>
            impactAnchor != null ? impactAnchor.position : transform.position + Vector3.up * 0.35f;

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

            _plateColor = isNear ? nearColor : farColor;

            Tint(plate, _plateColor);
            Tint(portrait, portraitColor);
            Tint(healthPlate, healthColor);
            Tint(armorPlate, armorColor);
        }

        /// <summary>
        /// Shows the numbers as they were at the moment of a hit.
        ///
        /// Armour first and then health, which is the order the engine applies
        /// them in and the order a player needs to read them in. Both values
        /// arrive already worked out; nothing here subtracts anything.
        /// </summary>
        public void ShowDamage(int remainingHealth, int remainingArmor)
        {
            SetText(healthText, remainingHealth.ToString());
            SetText(armorText, remainingArmor.ToString());

            if (armorBadge != null)
            {
                armorBadge.SetActive(remainingArmor > 0);
            }
        }

        /// <summary>A recoil and a flash, started on the view and not waited on.</summary>
        public void PlayHitFeedback(float duration)
        {
            if (_feedback != null)
            {
                StopCoroutine(_feedback);
                _feedback = null;
                RestPosition();
            }

            if (duration <= 0f || !isActiveAndEnabled)
            {
                return;
            }

            _feedback = StartCoroutine(HitFeedback(duration));
        }

        private IEnumerator HitFeedback(float duration)
        {
            if (!_hasResting)
            {
                _restingLocal = transform.localPosition;
                _hasResting = true;
            }

            Vector3 resting = _restingLocal;

            yield return Tweens.Over(duration, Easing.Linear, t =>
            {
                float decay = 1f - t;
                float wobble = Mathf.Sin(t * Mathf.PI * 5f) * hitRecoil * decay;

                transform.localPosition = resting + new Vector3(wobble, 0f, wobble * 0.3f);
                Tint(plate, Color.Lerp(_plateColor, hitFlashColor, Easing.Pulse(t)));
            });

            RestPosition();
            _feedback = null;
        }

        private void RestPosition()
        {
            if (_hasResting)
            {
                transform.localPosition = _restingLocal;
            }

            Tint(plate, _plateColor);
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
