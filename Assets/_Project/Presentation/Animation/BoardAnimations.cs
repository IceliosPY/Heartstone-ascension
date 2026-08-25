using System.Collections;
using CoH.Core.Events;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Minions arriving and minions leaving.
    ///
    /// A summon lays the row out first and then plays the new minion in, so the
    /// neighbours are already sliding aside as it lands. A death is the reverse:
    /// the minion plays out, is removed, and only then does the row close up.
    /// Both take their final positions from the same layout the board always
    /// uses, so an animation can never leave a minion somewhere the rules did
    /// not put it.
    /// </summary>
    public sealed class BoardAnimations : IEventAnimation
    {
        private readonly AnimationContext _context;

        public BoardAnimations(AnimationContext context)
        {
            _context = context;
        }

        public IEnumerator Play(GameEvent gameEvent) => gameEvent switch
        {
            MinionSummonedEvent summoned => Summon(summoned),
            MinionDiedEvent died => Death(died),
            HeroDiedEvent heroDied => HeroDeath(heroDied),
            _ => null
        };

        private IEnumerator Summon(MinionSummonedEvent summoned)
        {
            MatchPresenter presenter = _context.Presenter;

            // Creates the new view and gives every other minion in the row a new
            // slot to slide to.
            presenter.RelayoutBoards();
            _context.Sound(FeedbackSound.Summon);

            if (presenter.TryGetMinionView(summoned.MinionId, out MinionView view) && view != null)
            {
                yield return view.PlaySummon(_context.Timing.Summon);
            }

            presenter.RefreshHud();
        }

        /// <summary>
        /// The minion plays out where it stood, and the row closes afterwards.
        ///
        /// Its view is still here because nothing destroyed it when the event
        /// arrived: that is what lets a minion that died several events ago
        /// still be seen taking the blow that killed it.
        /// </summary>
        private IEnumerator Death(MinionDiedEvent died)
        {
            MatchPresenter presenter = _context.Presenter;

            _context.Sound(FeedbackSound.Death);

            if (presenter.TryGetMinionView(died.MinionId, out MinionView view) && view != null)
            {
                yield return view.PlayDeath(_context.Timing.Death);
            }

            presenter.RemoveMinionView(died.MinionId);
            presenter.RelayoutBoards();
        }

        private IEnumerator HeroDeath(HeroDiedEvent died)
        {
            _context.Sound(FeedbackSound.Death);

            if (_context.Presenter.TryGetHeroView(died.HeroId, out HeroView hero) && hero != null)
            {
                hero.PlayHitFeedback(_context.Timing.DamageFeedback);
            }

            yield return Tweens.Wait(_context.Timing.ImpactPause);
        }
    }
}
