using System.Collections;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Cards arriving and cards leaving: drawn, generated, burned, played.
    ///
    /// Every one of them works from what the event carries. A drawn card is
    /// named by the event, a burned one is named by the event even though it
    /// never reached a hand and exists nowhere in the state, and a played card
    /// is animated out of a hand that has already lost it. Nothing here reads
    /// the state hoping something is still in it.
    /// </summary>
    public sealed class CardAnimations : IEventAnimation
    {
        private readonly AnimationContext _context;

        public CardAnimations(AnimationContext context)
        {
            _context = context;
        }

        public IEnumerator Play(GameEvent gameEvent) => gameEvent switch
        {
            CardDrawnEvent drawn => Draw(drawn),
            CardBurnedEvent burned => Burn(burned),
            CardGeneratedEvent generated => Generate(generated),
            CardPlayedEvent played => Played(played),
            _ => null
        };

        /// <summary>
        /// The card leaves the deck, travels, and lands exactly where the fan
        /// was going to put it.
        ///
        /// Aiming at the pose the layout computed is what lets the hand take
        /// over at the end without anything jumping: the card is already there
        /// when it stops being an animation and starts being a hand card.
        /// </summary>
        private IEnumerator Draw(CardDrawnEvent drawn)
        {
            MatchPresenter presenter = _context.Presenter;
            float duration = _context.Timing.CardDraw;

            CardView view = presenter.SpawnLooseCardView(drawn.CardInstanceId, drawn.PlayerId);

            if (view == null)
            {
                presenter.RelayoutHands();
                yield break;
            }

            bool near = presenter.IsNear(drawn.PlayerId);
            Vector3 from = presenter.DeckPosition(drawn.PlayerId);

            view.transform.SetPositionAndRotation(from, _context.Camera.transform.rotation);
            view.transform.localScale = Vector3.one * 0.35f;

            _context.Sound(FeedbackSound.CardDraw);

            if (presenter.TryGetHandPose(
                drawn.PlayerId, drawn.CardInstanceId,
                out Vector3 position, out Quaternion rotation, out float scale))
            {
                yield return Tweens.PoseTo(
                    view.transform, position, rotation, Vector3.one * scale, duration, Easing.OutQuad);
            }

            // The hand takes the card over from here, and the others settle
            // around it.
            presenter.RelayoutHands();

            if (!near)
            {
                presenter.RefreshHeroes();
            }
        }

        /// <summary>
        /// An overdrawn card: it comes off the deck, fails to reach the hand and
        /// goes. It exists nowhere in the state, so the whole thing is built
        /// from the event.
        /// </summary>
        private IEnumerator Burn(CardBurnedEvent burned)
        {
            MatchPresenter presenter = _context.Presenter;
            float duration = _context.Timing.CardBurn;

            CardView view = presenter.SpawnLooseCardView(burned.CardInstanceId, burned.PlayerId);
            _context.Sound(FeedbackSound.CardBurn);

            if (view != null)
            {
                Vector3 from = presenter.DeckPosition(burned.PlayerId);
                view.transform.SetPositionAndRotation(from, _context.Camera.transform.rotation);
                view.transform.localScale = Vector3.one * 0.5f;

                _context.ShowNumber(from + Vector3.up * 0.4f, "BURNED", new Color(1f, 0.45f, 0.25f));

                Vector3 to = from + Vector3.up * 0.7f;

                yield return Tweens.Over(duration, Easing.OutQuad, t =>
                {
                    if (view == null)
                    {
                        return;
                    }

                    view.transform.position = Vector3.Lerp(from, to, t);
                    view.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 0.05f, t);
                    view.transform.Rotate(0f, 0f, 240f * Time.deltaTime, Space.Self);
                });
            }

            presenter.RemoveCardView(burned.CardInstanceId);
            presenter.RefreshHeroes();
        }

        /// <summary>
        /// A card made straight into a hand. Nothing travels, because there is
        /// nowhere for it to travel from.
        /// </summary>
        private IEnumerator Generate(CardGeneratedEvent generated)
        {
            _context.Sound(FeedbackSound.CardDraw);
            _context.Presenter.RelayoutHands();
            _context.Presenter.RefreshHeroes();
            yield break;
        }

        /// <summary>
        /// The card leaves the hand toward the board and goes, so that whatever
        /// arrives next reads as the same thing rather than as something popping
        /// into existence elsewhere.
        /// </summary>
        private IEnumerator Played(CardPlayedEvent played)
        {
            MatchPresenter presenter = _context.Presenter;
            float duration = _context.Timing.CardPlay;

            _context.Sound(FeedbackSound.CardPlay);

            if (presenter.TryGetCardView(played.CardInstanceId, out CardView view) && view != null)
            {
                Transform row = presenter.Anchors == null
                    ? null
                    : presenter.Anchors.Board(presenter.IsNear(played.PlayerId));

                Vector3 destination = row == null
                    ? view.transform.position
                    : row.position + Vector3.up * 0.55f;

                yield return Tweens.PoseTo(
                    view.transform,
                    destination,
                    _context.Camera.transform.rotation,
                    view.transform.localScale * 0.45f,
                    duration,
                    Easing.InOutQuad);
            }

            presenter.RemoveCardView(played.CardInstanceId);
            presenter.RelayoutHands();
            presenter.RefreshHud();
        }
    }
}
