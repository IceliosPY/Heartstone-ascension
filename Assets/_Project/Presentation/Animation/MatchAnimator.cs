using System.Collections;
using System.Collections.Generic;
using CoH.Core.Events;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Where an engine event meets the animation that shows it.
    ///
    /// One component wired into the scene, and behind it a short list of plain
    /// classes grouped by what they stage. Asking each in turn keeps this from
    /// becoming the thousand line switch that a single visualizer would grow
    /// into, without inventing an interface per event to avoid it: the whole
    /// registry is the list below.
    ///
    /// It stages events and nothing else. It applies no rule, changes no state,
    /// and every number it shows arrived inside the event it is showing.
    /// </summary>
    public sealed class MatchAnimator : MonoBehaviour, IEventVisualizer
    {
        [Header("Wiring")]
        [SerializeField] private GameSession session;
        [SerializeField] private MatchPresenter presenter;
        [SerializeField] private MatchHud hud;
        [SerializeField] private PresentationTiming timing;
        [SerializeField] private AudioFeedback audioFeedback;
        [SerializeField] private Camera matchCamera;

        [Header("Effects")]
        [SerializeField] private FloatingNumber floatingNumberPrefab;
        [SerializeField] private Transform effectLayer;

        private readonly List<IEventAnimation> _animations = new List<IEventAnimation>();

        private CombatAnimations _combat;
        private bool _built;

        /// <summary>How many events have been staged. Diagnostics and tests.</summary>
        internal int StagedCount { get; private set; }

        /// <summary>The timings in force, so a test can collapse them.</summary>
        internal PresentationTiming Timing => timing;

        private void Awake()
        {
            if (matchCamera == null)
            {
                matchCamera = Camera.main;
            }

            Build();
        }

        private void OnEnable()
        {
            Build();

            if (session != null && session.Queue != null)
            {
                session.Queue.AddVisualizer(this);
                session.Queue.Drained += OnDrained;
            }
        }

        private void OnDisable()
        {
            if (session != null && session.Queue != null)
            {
                session.Queue.Drained -= OnDrained;
            }
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            AnimationContext context = new AnimationContext(
                session, presenter, hud, timing, audioFeedback,
                floatingNumberPrefab, effectLayer, matchCamera);

            _combat = new CombatAnimations(context);

            _animations.Clear();
            _animations.Add(new CardAnimations(context));
            _animations.Add(new BoardAnimations(context));
            _animations.Add(_combat);
            _animations.Add(new MatchFlowAnimations(context));

            _built = true;
        }

        public IEnumerator Play(GameEvent gameEvent)
        {
            Build();

            for (int index = 0; index < _animations.Count; index++)
            {
                IEnumerator routine = _animations[index].Play(gameEvent);

                if (routine != null)
                {
                    StagedCount++;
                    return routine;
                }
            }

            return null;
        }

        /// <summary>
        /// The batch is over and the board is idle again. Anything an animation
        /// left leaning is put back, and the reconcile that follows confirms the
        /// rest.
        /// </summary>
        private void OnDrained()
        {
            _combat?.ReleaseAttacker();
        }
    }
}
