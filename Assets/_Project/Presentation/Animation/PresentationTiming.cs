using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Every duration the presentation uses, in one place.
    ///
    /// Pacing is a single design decision, and it cannot be tuned when it is
    /// spread as literals across fifteen scripts. Everything asks here, and a
    /// card game that feels sluggish is fixed by moving numbers on one
    /// component.
    ///
    /// <see cref="Speed"/> divides every duration, and <see cref="Instant"/>
    /// takes them all to zero. Instant is not a second code path: the same
    /// sequences run, the same order, and every tween applies its end state
    /// without spending a frame. It is what lets a test assert on what a
    /// sequence produced rather than on how long it took.
    /// </summary>
    public sealed class PresentationTiming : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("Divides every duration. Two is twice as fast.")]
        [SerializeField] private float speed = 1f;

        [Tooltip("Collapses every duration to zero. Sequences still run in order.")]
        [SerializeField] private bool instant;

        [Header("Cards")]
        [SerializeField] private float cardDraw = 0.34f;
        [SerializeField] private float cardBurn = 0.36f;
        [SerializeField] private float cardPlay = 0.26f;
        [SerializeField] private float snapBack = 0.16f;

        [Header("Board")]
        [SerializeField] private float summon = 0.28f;
        [SerializeField] private float boardRelayout = 0.18f;

        [Header("Combat")]
        [SerializeField] private float attackWindup = 0.11f;
        [SerializeField] private float attackTravel = 0.16f;
        [SerializeField] private float impactPause = 0.06f;
        [SerializeField] private float attackReturn = 0.16f;
        [SerializeField] private float damageFeedback = 0.22f;
        [SerializeField] private float death = 0.34f;

        [Header("Match")]
        [SerializeField] private float turnBanner = 0.7f;
        [SerializeField] private float manaFeedback = 0.16f;
        [SerializeField] private float gameEndDelay = 0.35f;
        [SerializeField] private float gameEndReveal = 0.45f;

        [Header("Floating numbers")]
        [SerializeField] private float floatingNumber = 0.6f;

        /// <summary>True while every duration is collapsed to zero.</summary>
        public bool IsInstant => instant;

        /// <summary>
        /// Turns an authored duration into the one to actually play. The only
        /// way a duration reaches an animation.
        /// </summary>
        public float Scale(float seconds) => instant ? 0f : seconds / Mathf.Max(0.05f, speed);

        public float CardDraw => Scale(cardDraw);

        public float CardBurn => Scale(cardBurn);

        public float CardPlay => Scale(cardPlay);

        public float SnapBack => Scale(snapBack);

        public float Summon => Scale(summon);

        public float BoardRelayout => Scale(boardRelayout);

        public float AttackWindup => Scale(attackWindup);

        public float AttackTravel => Scale(attackTravel);

        public float ImpactPause => Scale(impactPause);

        public float AttackReturn => Scale(attackReturn);

        public float DamageFeedback => Scale(damageFeedback);

        public float Death => Scale(death);

        public float TurnBanner => Scale(turnBanner);

        public float ManaFeedback => Scale(manaFeedback);

        public float GameEndDelay => Scale(gameEndDelay);

        public float GameEndReveal => Scale(gameEndReveal);

        public float FloatingNumber => Scale(floatingNumber);

        /// <summary>
        /// Collapses or restores every duration. Used by tests, which care what
        /// a sequence did and not how long it spent doing it.
        /// </summary>
        internal void SetInstant(bool value) => instant = value;

        internal void SetSpeed(float value) => speed = Mathf.Max(0.05f, value);

        /// <summary>
        /// Sets both at once, for the developer tools.
        ///
        /// Public because the debug panel lives in another assembly and turning
        /// the pacing up while hunting something is exactly what it is for. It
        /// changes how fast the game is shown and nothing about what it does.
        /// </summary>
        public void SetPlayback(float playbackSpeed, bool playInstantly)
        {
            speed = Mathf.Max(0.05f, playbackSpeed);
            instant = playInstantly;
        }
    }
}
