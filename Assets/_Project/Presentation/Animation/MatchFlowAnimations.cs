using System.Collections;
using CoH.Core.Events;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The shape of a match: turns starting, mana arriving, fatigue biting, and
    /// the end.
    ///
    /// The turn banner earns its place twice over. It tells a player on a shared
    /// screen that the board is now theirs, and it covers the moment the board
    /// swings round: the flip happens behind it, so what would otherwise be a
    /// jarring switch of sides is something the banner hands over.
    /// </summary>
    public sealed class MatchFlowAnimations : IEventAnimation
    {
        private readonly AnimationContext _context;

        public MatchFlowAnimations(AnimationContext context)
        {
            _context = context;
        }

        public IEnumerator Play(GameEvent gameEvent) => gameEvent switch
        {
            TurnStartedEvent started => TurnStarted(started),
            ManaCrystalGainedEvent _ => ManaChanged(),
            ManaRefilledEvent _ => ManaChanged(),
            ManaSpentEvent _ => ManaChanged(),
            FatigueDamageEvent fatigue => Fatigue(fatigue),
            GameEndedEvent ended => GameEnded(ended),
            _ => null
        };

        private IEnumerator TurnStarted(TurnStartedEvent started)
        {
            MatchHud hud = _context.Hud;
            float total = _context.Timing.TurnBanner;

            _context.Sound(FeedbackSound.TurnStart);

            if (hud != null)
            {
                hud.SetBannerText(MatchHud.Describe(started.PlayerId) + " TURN");
                yield return Tweens.Over(total * 0.28f, Easing.OutQuad, hud.SetBannerAlpha);
            }

            // The board changes sides here, under the banner.
            _context.Presenter.Rebuild();

            yield return Tweens.Wait(total * 0.36f);

            if (hud != null)
            {
                yield return Tweens.Over(total * 0.36f, Easing.InOutQuad, t => hud.SetBannerAlpha(1f - t));
                hud.SetBannerAlpha(0f);
            }
        }

        /// <summary>
        /// Mana is a readout rather than an animation for now, but it still
        /// takes its own beat so the number changes when a player is looking at
        /// it and not three events earlier.
        /// </summary>
        private IEnumerator ManaChanged()
        {
            _context.Presenter.RefreshHud();
            yield return Tweens.Wait(_context.Timing.ManaFeedback);
        }

        /// <summary>
        /// Says why the damage that follows is about to happen. The damage
        /// itself is a separate event and stages itself.
        /// </summary>
        private IEnumerator Fatigue(FatigueDamageEvent fatigue)
        {
            MatchPresenter presenter = _context.Presenter;

            if (presenter.TryGetHeroViewOf(fatigue.PlayerId, out HeroView hero) && hero != null)
            {
                _context.ShowNumber(
                    hero.ImpactPoint + Vector3.up * 0.55f,
                    "FATIGUE " + fatigue.Amount,
                    new Color(0.78f, 0.62f, 1f));
            }

            yield return Tweens.Wait(_context.Timing.ManaFeedback);
        }

        private IEnumerator GameEnded(GameEndedEvent ended)
        {
            MatchHud hud = _context.Hud;

            yield return Tweens.Wait(_context.Timing.GameEndDelay);

            _context.Sound(FeedbackSound.GameEnd);

            if (hud == null)
            {
                yield break;
            }

            hud.ShowResult(ended.Result);

            yield return Tweens.Over(_context.Timing.GameEndReveal, Easing.OutBack, hud.SetResultReveal);

            hud.SetResultReveal(1f);
        }
    }
}
