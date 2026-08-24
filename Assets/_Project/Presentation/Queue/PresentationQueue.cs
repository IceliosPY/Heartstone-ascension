using System;
using System.Collections;
using System.Collections.Generic;
using CoH.Core.Events;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>Something that turns one engine event into something visible.</summary>
    public interface IEventVisualizer
    {
        /// <summary>Shows the event. Returns true when it handled it.</summary>
        bool Handle(GameEvent gameEvent);
    }

    /// <summary>
    /// Replays the engine's events one at a time.
    ///
    /// The engine resolves a whole command instantly and hands back an ordered
    /// list. The presentation is deliberately slower: it walks that list frame
    /// by frame, so a trade reads as an attack, then two impacts, then two
    /// deaths, instead of everything changing between two frames.
    ///
    /// Input is locked while the queue runs. That is not polish, it is
    /// correctness: without it a player could click during a resolution and act
    /// on a board that has already moved on.
    ///
    /// Today every visualizer finishes immediately. When animations arrive in a
    /// later phase they slot in here, and nothing else has to change.
    /// </summary>
    public sealed class PresentationQueue : MonoBehaviour
    {
        [Tooltip("Frames to wait between two events. Zero plays a whole batch in one frame.")]
        [SerializeField] private int framesBetweenEvents = 1;

        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private readonly List<IEventVisualizer> _visualizers = new List<IEventVisualizer>();

        private Coroutine _playback;

        /// <summary>Raised once the queue has drained, so the world can settle.</summary>
        public event Action Drained;

        /// <summary>True while events are being replayed. Input must stay locked.</summary>
        public bool IsPlaying => _playback != null;

        public void AddVisualizer(IEventVisualizer visualizer)
        {
            if (visualizer != null && !_visualizers.Contains(visualizer))
            {
                _visualizers.Add(visualizer);
            }
        }

        public void Enqueue(IReadOnlyList<GameEvent> events)
        {
            if (events == null)
            {
                return;
            }

            for (int index = 0; index < events.Count; index++)
            {
                _pending.Enqueue(events[index]);
            }

            if (!IsPlaying && isActiveAndEnabled)
            {
                _playback = StartCoroutine(Play());
            }
        }

        /// <summary>
        /// Drains everything now, without waiting for frames. Used by the
        /// initial snapshot and by tests, which have no interest in pacing.
        /// </summary>
        public void FlushImmediately()
        {
            if (_playback != null)
            {
                StopCoroutine(_playback);
                _playback = null;
            }

            while (_pending.Count > 0)
            {
                Dispatch(_pending.Dequeue());
            }

            Drained?.Invoke();
        }

        private IEnumerator Play()
        {
            while (_pending.Count > 0)
            {
                Dispatch(_pending.Dequeue());

                for (int frame = 0; frame < framesBetweenEvents; frame++)
                {
                    yield return null;
                }
            }

            _playback = null;
            Drained?.Invoke();
        }

        private void Dispatch(GameEvent gameEvent)
        {
            for (int index = 0; index < _visualizers.Count; index++)
            {
                if (_visualizers[index].Handle(gameEvent))
                {
                    return;
                }
            }
        }
    }
}
