using System.Collections;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// An attack, and the damage it causes.
    ///
    /// Two minions trading blows hurt each other at the same instant as far as
    /// the rules are concerned, but the engine reports the two hits one after
    /// the other, because a list has an order and a moment does not. Staging
    /// them strictly one after the other would turn a trade into "A hits B, and
    /// then B mysteriously hurts A".
    ///
    /// So a hit starts its recoil and flash on the view and is not waited on.
    /// The sequence pauses only for a beat before the next event, which lands
    /// while the first flash is still running. Both minions light up together,
    /// and nothing had to be reordered or merged to arrange it.
    ///
    /// The attacker is left leaning into its target rather than snapped back,
    /// and finds its way home on its own once the extension is released: minion
    /// views always slide toward the slot the layout wants. It is released when
    /// the next attack begins or when the batch ends, so the return happens
    /// under the impacts and the deaths instead of before them.
    /// </summary>
    public sealed class CombatAnimations : IEventAnimation
    {
        private const float WindupDistance = 0.22f;

        /// <summary>How far toward the target the attacker leans. Not all the way.</summary>
        private const float LungeFraction = 0.55f;

        private readonly AnimationContext _context;

        private MinionView _extended;

        public CombatAnimations(AnimationContext context)
        {
            _context = context;
        }

        public IEnumerator Play(GameEvent gameEvent) => gameEvent switch
        {
            AttackDeclaredEvent declared => Attack(declared),
            DamageDealtEvent damage => Damage(damage),
            _ => null
        };

        /// <summary>
        /// Puts any leaning attacker back. Called when a batch finishes, so a
        /// minion is never left off its slot once the board is idle.
        /// </summary>
        public void ReleaseAttacker()
        {
            if (_extended != null)
            {
                _extended.SetLungeOffset(Vector3.zero);
            }

            _extended = null;
        }

        private IEnumerator Attack(AttackDeclaredEvent declared)
        {
            ReleaseAttacker();

            MatchPresenter presenter = _context.Presenter;
            _context.Sound(FeedbackSound.Attack);

            if (!presenter.TryGetMinionView(declared.AttackerId, out MinionView attacker) || attacker == null)
            {
                yield break;
            }

            ICombatTargetView target = _context.FindCombatTarget(declared.TargetId);

            if (target == null)
            {
                yield break;
            }

            Transform row = attacker.transform.parent;
            Vector3 worldDelta = target.ImpactPoint - attacker.ImpactPoint;

            // Worked out in the row's own space, because that is the space the
            // offset is applied in and the row can be moved or turned around.
            Vector3 towardTarget = row == null ? worldDelta : row.InverseTransformVector(worldDelta);
            Vector3 direction = towardTarget.sqrMagnitude > 0.0001f
                ? towardTarget.normalized
                : Vector3.forward;

            // A short pull back, which is what makes the lunge read as a swing
            // rather than a slide.
            yield return Tweens.Over(_context.Timing.AttackWindup, Easing.InOutQuad, t =>
            {
                if (attacker != null)
                {
                    attacker.SetLungeOffset(-direction * (WindupDistance * t));
                }
            });

            Vector3 windup = -direction * WindupDistance;
            Vector3 lunge = towardTarget * LungeFraction;

            yield return Tweens.Over(_context.Timing.AttackTravel, Easing.OutQuad, t =>
            {
                if (attacker != null)
                {
                    attacker.SetLungeOffset(Vector3.LerpUnclamped(windup, lunge, t));
                }
            });

            _extended = attacker;
        }

        /// <summary>
        /// One hit landing: the numbers change to what they were at that moment,
        /// the view reacts, and the sequence moves on after a beat.
        ///
        /// Every number comes from the event. The state has already finished the
        /// whole exchange, and the target may not even be on the board any more.
        /// </summary>
        private IEnumerator Damage(DamageDealtEvent damage)
        {
            ICombatTargetView target = _context.FindCombatTarget(damage.TargetId);

            _context.Sound(FeedbackSound.Impact);

            if (target != null)
            {
                target.ShowDamage(damage.RemainingHealth, damage.RemainingArmor);

                // Started, not awaited. This is what lets both halves of a trade
                // flash together.
                target.PlayHitFeedback(_context.Timing.DamageFeedback);

                _context.ShowNumber(
                    target.ImpactPoint + Vector3.up * 0.25f,
                    "-" + damage.Amount,
                    damage.AbsorbedByArmor > 0 ? new Color(0.62f, 0.80f, 1f) : new Color(1f, 0.42f, 0.36f));
            }

            _context.Presenter.RefreshHud();

            yield return Tweens.Wait(_context.Timing.ImpactPause);
        }
    }
}
