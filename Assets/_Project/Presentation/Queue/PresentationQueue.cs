using System;
using System.Collections;
using System.Collections.Generic;
using CoH.Core.Events;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>Something that stages one engine event.</summary>
    public interface IEventVisualizer
    {
        /// <summary>
        /// A coroutine that shows the event, or null when this visualizer has
        /// nothing to say about it.
        ///
        /// Returning a coroutine rather than a bool is what makes the queue
        /// temporal: it waits for what it started, so an attack finishes
        /// travelling before the impact it causes is shown.
        /// </summary>
        IEnumerator Play(GameEvent gameEvent);
    }

    /// <summary>
    /// Stages the engine's events, one after another.
    ///
    /// The engine resolves a whole command between two lines of code and hands
    /// back an ordered list. This walks that list and waits for each event to
    /// finish being shown before starting the next, so a trade reads as a lunge,
    /// then two impacts, then two deaths, instead of a board that changes
    /// between two frames.
    ///
    /// Strictly sequential, and deliberately so. Events that are genuinely
    /// simultaneous could later be grouped and played together, but that is a
    /// decision about which events those are, not machinery to build in advance.
    ///
    /// The presentation is therefore behind the engine for as long as a sequence
    /// lasts. That is the point. Input stays locked throughout, so nobody can
    /// act on a board that has already moved on.
    /// </summary>
    public sealed class PresentationQueue : MonoBehaviour
    {
        private readonly Queue<GameEvent> _pending = new Queue<GameEvent>();
        private readonly List<IEventVisualizer> _visualizers = new List<IEventVisualizer>();

        private Coroutine _playback;

        /// <summary>Raised once the queue has drained, so the world can settle.</summary>
        public event Action Drained;

        /// <summary>
        /// Raised just before an event is staged.
        ///
        /// The one place to watch a sequence unfold from the outside. What a
        /// staged sequence is worth is largely in its order and in what still
        /// exists at each step, and neither can be seen by looking at the board
        /// once everything has finished.
        /// </summary>
        public event Action<GameEvent> Staging;

        /// <summary>True while events are being staged. Input must stay locked.</summary>
        public bool IsPlaying => _playback != null;

        /// <summary>How many events are still waiting. Diagnostics and tests.</summary>
        public int PendingCount => _pending.Count;

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
        /// Stages everything now, without waiting.
        ///
        /// Not a second code path: it runs the very same sequences, and they
        /// finish inside one call because every duration they ask for has been
        /// collapsed to zero. Used by the opening snapshot and by tests.
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
                IEnumerator routine = Stage(_pending.Dequeue());

                if (routine != null)
                {
                    Drain(routine);
                }
            }

            Drained?.Invoke();
        }

        private IEnumerator Play()
        {
            while (_pending.Count > 0)
            {
                IEnumerator routine = Stage(_pending.Dequeue());

                if (routine != null)
                {
                    yield return StartCoroutine(routine);
                }
            }

            _playback = null;
            Drained?.Invoke();
        }

        /// <summary>
        /// The first visualizer that claims the event stages it. Nothing claims
        /// an event the presentation has no opinion about, and the queue simply
        /// moves on.
        /// </summary>
        private IEnumerator Stage(GameEvent gameEvent)
        {
            Staging?.Invoke(gameEvent);

            for (int index = 0; index < _visualizers.Count; index++)
            {
                IEnumerator routine = _visualizers[index].Play(gameEvent);

                if (routine != null)
                {
                    return routine;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs a coroutine to its end without yielding to Unity.
        ///
        /// Only safe because instant sequences never wait on anything but their
        /// own nested enumerators, which is exactly what a zero duration
        /// guarantees. The guard is there so a mistake shows up as a warning
        /// rather than as a frozen editor.
        /// </summary>
        private static void Drain(IEnumerator routine)
        {
            const int limit = 100000;
            int steps = 0;

            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);

            while (stack.Count > 0 && steps++ < limit)
            {
                IEnumerator current = stack.Peek();

                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }

            if (steps >= limit)
            {
                Debug.LogWarning("A presentation sequence did not finish instantly. Check its durations.");
            }
        }
    }
}
