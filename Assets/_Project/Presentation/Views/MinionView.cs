using System.Collections;
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
    public sealed class MinionView : MonoBehaviour, ICombatTargetView
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

        [Header("Feedback")]
        [SerializeField] private Transform impactAnchor;

        [SerializeField] private Color hitFlashColor = new Color(1f, 0.86f, 0.72f);

        [Tooltip("How quickly a minion slides to the place the row layout wants it.")]
        [SerializeField] private float poseSmoothing = 20f;

        [SerializeField] private float hitRecoil = 0.11f;

        private MaterialPropertyBlock _block;
        private bool _canAttack;
        private Renderer _targetRingRenderer;
        private Vector3 _targetRingScale = Vector3.one;

        private Vector3 _restingLocal;
        private Vector3 _offsetLocal;
        private bool _hasPose;
        private Transform _poseParent;
        private Color _bodyColor;
        private Coroutine _feedback;

        private void Awake()
        {
            if (targetRing != null)
            {
                _targetRingRenderer = targetRing.GetComponent<Renderer>();
                _targetRingScale = targetRing.transform.localScale;
            }

            _bodyColor = restingColor;
        }

        /// <summary>Where an attack should land on this minion.</summary>
        public Vector3 ImpactPoint =>
            impactAnchor != null ? impactAnchor.position : transform.position + Vector3.up * 0.25f;

        /// <summary>Where the row layout wants this minion to stand.</summary>
        public Vector3 RestingLocalPosition => _restingLocal;

        /// <summary>
        /// Records the slot the row layout computed.
        ///
        /// A minion slides to a new slot when the row re-lays out around it, and
        /// arrives instantly when it is new or when the board has just flipped.
        /// It is the same rule cards follow, and it is what makes a summon push
        /// its neighbours aside rather than teleport them.
        /// </summary>
        public void SetRestingPose(Vector3 localPosition)
        {
            _restingLocal = localPosition;

            bool snap = !_hasPose || _poseParent != transform.parent;

            _hasPose = true;
            _poseParent = transform.parent;

            if (snap)
            {
                transform.localPosition = _restingLocal + _offsetLocal;
            }
        }

        /// <summary>
        /// A temporary displacement, in the row's own space, for a lunge or a
        /// recoil. Clearing it lets the minion find its way home on its own.
        /// </summary>
        public void SetVisualOffset(Vector3 offsetLocal) => _offsetLocal = offsetLocal;

        public Vector3 VisualOffset => _offsetLocal;

        private void LateUpdate()
        {
            if (!_hasPose)
            {
                return;
            }

            if (_offsetLocal.sqrMagnitude > 0.000001f)
            {
                // An animation is driving it, and that animation is already
                // eased. Smoothing on top would only add lag to a lunge.
                transform.localPosition = _restingLocal + _offsetLocal;
                return;
            }

            float t = 1f - Mathf.Exp(-poseSmoothing * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, _restingLocal, t);
        }

        /// <summary>
        /// Shows health as it was at the moment of a hit. The engine may already
        /// hold a lower number, or none at all if this minion is about to be
        /// removed; what a player is watching is this.
        /// </summary>
        public void ShowDamage(int remainingHealth, int remainingArmor)
        {
            SetText(healthText, remainingHealth.ToString());
            Tint(healthPlate, hurtPlateColor);
        }

        /// <summary>
        /// A recoil and a flash, started on the view and not waited on.
        ///
        /// Not waiting is what lets two minions trading blows light up together
        /// even though their two damage events are staged one after the other.
        /// </summary>
        public void PlayHitFeedback(float duration)
        {
            if (_feedback != null)
            {
                StopCoroutine(_feedback);
                _feedback = null;
                SetVisualOffset(Vector3.zero);
            }

            if (duration <= 0f || !isActiveAndEnabled)
            {
                return;
            }

            _feedback = StartCoroutine(HitFeedback(duration));
        }

        private IEnumerator HitFeedback(float duration)
        {
            Vector3 held = _offsetLocal;

            yield return Tweens.Over(duration, Easing.Linear, t =>
            {
                float decay = 1f - t;
                float wobble = Mathf.Sin(t * Mathf.PI * 5f) * hitRecoil * decay;

                SetVisualOffset(held + new Vector3(wobble, 0f, wobble * 0.35f));
                Tint(body, Color.Lerp(_bodyColor, hitFlashColor, Easing.Pulse(t)));
            });

            SetVisualOffset(held);
            Tint(body, _bodyColor);
            _feedback = null;
        }

        /// <summary>
        /// The minion leaving the board: it sinks, shrinks and turns away. The
        /// view is still destroyed by whoever started this, once it ends.
        /// </summary>
        public IEnumerator PlayDeath(float duration)
        {
            SetSelected(false);
            SetTargetable(false);

            Vector3 fromScale = transform.localScale;
            Vector3 startLocal = _restingLocal + _offsetLocal;
            Quaternion fromRotation = transform.localRotation;

            // Stop the layout from fighting the animation for the transform.
            _hasPose = false;

            yield return Tweens.Over(duration, Easing.OutQuad, t =>
            {
                transform.localScale = Vector3.LerpUnclamped(fromScale, fromScale * 0.15f, t);
                transform.localPosition = startLocal + new Vector3(0f, -0.35f * t, 0f);
                transform.localRotation = fromRotation * Quaternion.Euler(0f, 140f * t, 0f);

                Tint(body, Color.Lerp(_bodyColor, new Color(0.16f, 0.13f, 0.13f), t));
            });
        }

        /// <summary>Arrives on the board: small, then settling into place.</summary>
        public IEnumerator PlaySummon(float duration)
        {
            transform.localScale = Vector3.zero;

            yield return Tweens.Over(duration, Easing.OutBack, t =>
                transform.localScale = Vector3.one * t);

            transform.localScale = Vector3.one;
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

            _bodyColor = _canAttack ? readyColor : restingColor;

            Tint(body, _bodyColor);
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
