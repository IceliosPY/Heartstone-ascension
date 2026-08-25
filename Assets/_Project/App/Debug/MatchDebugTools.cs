using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Events;
using CoH.Presentation;
using UnityEngine;

namespace CoH.App
{
    /// <summary>
    /// The development side of a match: what was submitted, what came back, and
    /// the ability to start again from a prepared position or a recording.
    ///
    /// It watches and it orchestrates; it never plays. Every command it sends
    /// goes through the same GameSession a player uses, so a replay on screen
    /// takes the same path as the match it is replaying, animations and all.
    /// There is no second renderer and no privileged route into the engine.
    ///
    /// The recording is kept up to date without anyone asking for it. Having to
    /// remember to press record before the interesting thing happens is how
    /// bugs get away.
    /// </summary>
    public sealed class MatchDebugTools : MonoBehaviour
    {
        [SerializeField] private MatchBootstrap bootstrap;
        [SerializeField] private GameSession session;
        [SerializeField] private MatchPresenter presenter;
        [SerializeField] private PresentationTiming timing;

        [Tooltip("How many commands and events the panel keeps in view.")]
        [SerializeField] private int historyLength = 40;

        private readonly List<string> _eventHistory = new List<string>();

        private ReplayRecorder _recorder;
        private Coroutine _replay;

        /// <summary>The recording of the match currently being played.</summary>
        public ReplayRecord Recording => _recorder?.Record;

        /// <summary>Where the recording started from, in words.</summary>
        public string SourceDescription { get; private set; } = "match";

        /// <summary>The last verification that was run, or null.</summary>
        public ReplayVerificationResult LastVerification { get; private set; }

        /// <summary>True while a recording is being played back on screen.</summary>
        public bool IsReplaying => _replay != null;

        /// <summary>How far through a visual replay we are.</summary>
        public int ReplayPosition { get; private set; }

        public int ReplayLength { get; private set; }

        public MatchBootstrap Bootstrap => bootstrap;

        public GameSession Session => session;

        public MatchPresenter Presenter => presenter;

        public PresentationTiming Timing => timing;

        /// <summary>The events of the last few commands, newest last.</summary>
        public IReadOnlyList<string> EventHistory => _eventHistory;

        private void OnEnable()
        {
            if (session != null)
            {
                session.CommandExecuted += OnCommandExecuted;
            }

            if (bootstrap != null)
            {
                bootstrap.MatchReplaced += OnMatchReplaced;
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.CommandExecuted -= OnCommandExecuted;
            }

            if (bootstrap != null)
            {
                bootstrap.MatchReplaced -= OnMatchReplaced;
            }
        }

        // ------------------------------------------------------------------
        //  Recording
        // ------------------------------------------------------------------

        private void OnMatchReplaced()
        {
            // Whatever was recorded described a match that no longer exists.
            StartRecording(SourceDescription);
        }

        /// <summary>Begins a fresh recording of whatever the session is serving now.</summary>
        public void StartRecording(string source)
        {
            _eventHistory.Clear();
            LastVerification = null;

            if (bootstrap == null || bootstrap.RuntimeCatalog == null)
            {
                _recorder = null;
                return;
            }

            SourceDescription = source ?? "match";

            if (source != null && source.StartsWith("scenario:", System.StringComparison.Ordinal))
            {
                _recorder = ReplayRecorder.ForScenario(
                    source.Substring("scenario:".Length),
                    bootstrap.RuntimeCatalog,
                    bootstrap.Config,
                    System.DateTime.UtcNow.ToString("u"));

                return;
            }

            _recorder = ReplayRecorder.ForMatch(
                bootstrap.Seed, bootstrap.DeckOne, bootstrap.DeckTwo,
                bootstrap.RuntimeCatalog, bootstrap.Config,
                System.DateTime.UtcNow.ToString("u"),
                bootstrap.HostMulligans);
        }

        private void OnCommandExecuted(GameCommand command, CommandResult result)
        {
            if (_recorder == null || session == null || !session.IsReady)
            {
                return;
            }

            _recorder.Observe(command, result, session.State);

            for (int index = 0; index < result.Events.Count; index++)
            {
                _eventHistory.Add(EventFingerprint.Describe(result.Events[index]));
            }

            int excess = _eventHistory.Count - Mathf.Max(4, historyLength);

            if (excess > 0)
            {
                _eventHistory.RemoveRange(0, excess);
            }
        }

        // ------------------------------------------------------------------
        //  Verifying
        // ------------------------------------------------------------------

        /// <summary>
        /// Replays the current recording in an engine of its own and compares
        /// every step.
        ///
        /// The match on screen is not touched. The verifier builds its own
        /// engine from the same inputs and throws it away afterwards, which is
        /// the only way the answer means anything.
        /// </summary>
        public ReplayVerificationResult VerifyCurrentReplay()
        {
            if (_recorder == null || bootstrap == null || bootstrap.RuntimeCatalog == null)
            {
                LastVerification = ReplayVerificationResult.Diverged(
                    DivergenceKind.ReplayFailed, -1, 0, "a recording", "nothing recorded yet");

                return LastVerification;
            }

            LastVerification = ReplayVerifier.Verify(_recorder.Record, bootstrap.RuntimeCatalog);

            if (!LastVerification.Success)
            {
                // The one thing worth putting in the console unprompted.
                Debug.LogWarning("Replay verification failed.\n" + LastVerification.Describe(), this);
            }

            return LastVerification;
        }

        public ReplayVerificationResult Verify(ReplayRecord record)
        {
            if (record == null || bootstrap == null || bootstrap.RuntimeCatalog == null)
            {
                LastVerification = ReplayVerificationResult.Diverged(
                    DivergenceKind.ReplayFailed, -1, 0, "a replay", "nothing to verify");

                return LastVerification;
            }

            LastVerification = ReplayVerifier.Verify(record, bootstrap.RuntimeCatalog);
            return LastVerification;
        }

        // ------------------------------------------------------------------
        //  Files
        // ------------------------------------------------------------------

        /// <summary>Writes the current recording out. Returns the path, or empty.</summary>
        public string ExportCurrentReplay()
        {
            if (_recorder == null)
            {
                return string.Empty;
            }

            string label = _recorder.Record.InitialSource == ReplayInitialSource.Scenario
                ? _recorder.Record.ScenarioId
                : "match";

            string path = ReplayFiles.Save(_recorder.Record, label);

            // Worth a line: a file was written somewhere the player cannot see.
            Debug.Log("Replay exported to " + path, this);
            return path;
        }

        /// <summary>Writes the readable state dump out. Returns the path, or empty.</summary>
        public string ExportStateDump()
        {
            if (session == null || !session.IsReady)
            {
                return string.Empty;
            }

            string path = ReplayFiles.SaveText("state", StateDump.Readable(session.State));
            Debug.Log("State dump written to " + path, this);
            return path;
        }

        // ------------------------------------------------------------------
        //  Scenarios and visual replay
        // ------------------------------------------------------------------

        public bool LoadScenario(string scenarioId)
        {
            StopReplay();
            SourceDescription = "scenario:" + scenarioId;

            if (bootstrap != null && bootstrap.LoadScenario(scenarioId))
            {
                return true;
            }

            SourceDescription = "match";
            return false;
        }

        public void RestartMatch()
        {
            StopReplay();
            SourceDescription = "match";
            bootstrap?.RestartMatch();
        }

        /// <summary>
        /// Plays a recording back on screen, one command at a time, through the
        /// same session a player uses.
        ///
        /// Every command produces its real events, the presentation queue stages
        /// them exactly as it always does, and the animations are the ones the
        /// game has. Nothing here draws anything.
        /// </summary>
        public void PlayReplay(ReplayRecord record)
        {
            if (record == null || bootstrap == null || !isActiveAndEnabled)
            {
                return;
            }

            StopReplay();

            if (!bootstrap.LoadReplayStart(record))
            {
                return;
            }

            _replay = StartCoroutine(RunReplay(record));
        }

        public void StopReplay()
        {
            if (_replay != null)
            {
                StopCoroutine(_replay);
                _replay = null;
            }

            ReplayPosition = 0;
            ReplayLength = 0;
        }

        private IEnumerator RunReplay(ReplayRecord record)
        {
            ReplayLength = record.CommandCount;
            ReplayPosition = 0;

            // The replay is what is being recorded now, so the panel shows the
            // run rather than the recording it came from.
            StartRecording(record.InitialSource == ReplayInitialSource.Scenario
                ? "scenario:" + record.ScenarioId
                : "match");

            for (int index = 0; index < record.Entries.Count; index++)
            {
                while (session.IsBusy)
                {
                    yield return null;
                }

                session.Submit(record.Entries[index].Command.ToCommand());
                ReplayPosition = index + 1;

                yield return null;
            }

            while (session.IsBusy)
            {
                yield return null;
            }

            _replay = null;
        }
    }
}
