using System.Collections;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>One family of events, staged.</summary>
    public interface IEventAnimation
    {
        /// <summary>
        /// A coroutine that shows the event, or null when this animation has
        /// nothing to do with it.
        /// </summary>
        IEnumerator Play(GameEvent gameEvent);
    }

    /// <summary>
    /// Everything an animation needs, gathered once.
    ///
    /// Passed to each animation rather than serialised on each of them, so the
    /// scene is wired in one place and the animations stay plain classes that
    /// happen to return coroutines. They own no scene references, spawn nothing
    /// directly and reach the rules only through the session, which is what
    /// keeps a staged sequence from turning into a second copy of the game.
    /// </summary>
    public sealed class AnimationContext
    {
        public AnimationContext(
            GameSession session,
            MatchPresenter presenter,
            MatchHud hud,
            PresentationTiming timing,
            AudioFeedback audio,
            FloatingNumber numberPrefab,
            Transform numberLayer,
            Camera matchCamera)
        {
            Session = session;
            Presenter = presenter;
            Hud = hud;
            Timing = timing;
            Audio = audio;
            NumberPrefab = numberPrefab;
            NumberLayer = numberLayer;
            Camera = matchCamera;
        }

        public GameSession Session { get; }

        public MatchPresenter Presenter { get; }

        public MatchHud Hud { get; }

        public PresentationTiming Timing { get; }

        public AudioFeedback Audio { get; }

        public FloatingNumber NumberPrefab { get; }

        public Transform NumberLayer { get; }

        public Camera Camera { get; }

        public GameState State => Session == null || !Session.IsReady ? null : Session.State;

        public void Sound(FeedbackSound sound)
        {
            if (Audio != null)
            {
                Audio.Play(sound);
            }
        }

        /// <summary>
        /// The view of whatever an attack can land on, minion or hero alike.
        /// Null when the thing has already left the board, which is a normal
        /// thing to happen in a sequence the engine finished long ago.
        /// </summary>
        public ICombatTargetView FindCombatTarget(EntityId id)
        {
            if (Presenter == null)
            {
                return null;
            }

            if (Presenter.TryGetMinionView(id, out MinionView minion) && minion != null)
            {
                return minion;
            }

            if (Presenter.TryGetHeroView(id, out HeroView hero) && hero != null)
            {
                return hero;
            }

            return null;
        }

        /// <summary>
        /// A number that rises off a character and fades. Purely presentation,
        /// started and left to remove itself.
        /// </summary>
        public void ShowNumber(Vector3 worldPosition, string text, Color colour)
        {
            if (NumberPrefab == null || NumberLayer == null || Timing == null)
            {
                return;
            }

            float duration = Timing.FloatingNumber;

            if (duration <= 0f)
            {
                // Instant presentation shows nothing that only exists over time.
                return;
            }

            FloatingNumber number = Object.Instantiate(NumberPrefab, NumberLayer);
            number.Show(worldPosition, text, colour, duration, Camera);
        }
    }
}
