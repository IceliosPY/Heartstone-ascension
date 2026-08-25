using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// Plays a recorded match again, in a fresh engine, and compares.
    ///
    /// It does not apply the recorded events to a state. It re-executes the
    /// commands and lets the engine produce its own events, then checks that
    /// they match. That difference is the whole point: applying the recording
    /// would reproduce the match without testing anything, whereas re-running
    /// it means a replay that still matches is proof the engine is
    /// deterministic, and one that does not has found something.
    ///
    /// It stops at the first difference. A divergence usually causes every
    /// later command to land in a different position, so continuing turns one
    /// finding into a hundred, and the first one is the only one worth reading.
    ///
    /// Nothing it does touches the match being played. The engine it builds is
    /// its own, and it is thrown away afterwards.
    /// </summary>
    public static class ReplayVerifier
    {
        public static ReplayVerificationResult Verify(ReplayRecord record, ICardCatalog catalog)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (record.FormatVersion != ReplayFormat.CurrentVersion)
            {
                return ReplayVerificationResult.Diverged(
                    DivergenceKind.ReplayFormatMismatch, -1, 0,
                    "format version " + ReplayFormat.CurrentVersion,
                    "format version " + record.FormatVersion);
            }

            string catalogNow = CatalogFingerprint.Of(catalog);

            if (record.CatalogFingerprint.Length > 0 &&
                !string.Equals(record.CatalogFingerprint, catalogNow, StringComparison.Ordinal))
            {
                // Worth its own kind: the replay is fine, the cards changed
                // underneath it, and no amount of staring at command #17 will
                // explain that.
                return ReplayVerificationResult.Diverged(
                    DivergenceKind.CatalogMismatch, -1, 0,
                    "catalog " + record.CatalogFingerprint,
                    "catalog " + catalogNow);
            }

            GameEngine engine;

            try
            {
                engine = BuildEngine(record, catalog);
            }
            catch (ArgumentException error)
            {
                return ReplayVerificationResult.Diverged(
                    DivergenceKind.UnknownScenario, -1, 0,
                    "scenario '" + record.ScenarioId + "'", error.Message);
            }
            catch (Exception error)
            {
                return ReplayVerificationResult.Diverged(
                    DivergenceKind.ReplayFailed, -1, 0,
                    "a match to replay into", error.Message);
            }

            return Compare(record, engine);
        }

        /// <summary>Rebuilds the position the recording started from.</summary>
        public static GameEngine BuildEngine(ReplayRecord record, ICardCatalog catalog)
        {
            GameConfig config = record.Config.ToConfig();

            if (record.InitialSource == ReplayInitialSource.Scenario)
            {
                DebugScenario scenario = DebugScenarios.Find(record.ScenarioId);
                return GameEngine.FromState(DebugScenarioBuilder.Build(scenario, catalog, config));
            }

            GameEngine engine = new GameEngine(config, catalog, record.Seed);
            engine.StartMatch(new DeckList(record.DeckOne), new DeckList(record.DeckTwo));

            // The opening exchange was settled by the host before anybody could
            // act, so it is not one of the recorded commands. Without applying
            // it again the fresh match would still be waiting for a mulligan
            // and would refuse the first thing the replay asked of it.
            for (int index = 0; index < record.MulliganChoices.Count; index++)
            {
                ReplayMulligan mulligan = record.MulliganChoices[index];
                engine.Execute(new MulliganCommand(mulligan.PlayerId, mulligan.CardsToReplace));
            }

            return engine;
        }

        private static ReplayVerificationResult Compare(ReplayRecord record, GameEngine engine)
        {
            for (int index = 0; index < record.Entries.Count; index++)
            {
                ReplayEntry expected = record.Entries[index];

                CommandResult actual;
                GameCommand command;

                try
                {
                    command = expected.Command.ToCommand();
                    actual = engine.Execute(command);
                }
                catch (Exception error)
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.ReplayFailed, expected.Sequence, index,
                        "the command to be executable", error.Message,
                        expected.Command.Describe());
                }

                if (actual.IsAccepted != expected.Accepted)
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.CommandResultMismatch, expected.Sequence, index,
                        expected.Accepted ? "accepted" : "rejected (" + expected.Reason + ")",
                        actual.IsAccepted ? "accepted" : "rejected (" + actual.Reason + ")",
                        expected.Command.Describe());
                }

                if (!actual.IsAccepted && actual.Reason != expected.Reason)
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.RejectionReasonMismatch, expected.Sequence, index,
                        expected.Reason.ToString(), actual.Reason.ToString(),
                        expected.Command.Describe());
                }

                ReplayVerificationResult events = CompareEvents(expected, actual.Events, index);

                if (events != null)
                {
                    return events;
                }

                string stateNow = StateFingerprint.Of(engine.State);

                if (expected.StateFingerprint.Length > 0 &&
                    !string.Equals(expected.StateFingerprint, stateNow, StringComparison.Ordinal))
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.StateFingerprintMismatch, expected.Sequence, index,
                        expected.StateFingerprint, stateNow,
                        expected.Command.Describe(),
                        expectedDraws: -1,
                        actualDraws: engine.State.RandomSource.DrawCount);
                }
            }

            return ReplayVerificationResult.Deterministic(record.Entries.Count);
        }

        /// <summary>
        /// Compares the reported events line by line, and names the first one
        /// that differs rather than only saying the batch did.
        /// </summary>
        private static ReplayVerificationResult CompareEvents(
            ReplayEntry expected, IReadOnlyList<GameEvent> actual, int checkedSoFar)
        {
            if (expected.EventCount != actual.Count)
            {
                return ReplayVerificationResult.Diverged(
                    DivergenceKind.EventMismatch, expected.Sequence, checkedSoFar,
                    expected.EventCount + " events", actual.Count + " events",
                    expected.Command.Describe());
            }

            // An older recording may hold only the hash. Fall back to it rather
            // than skipping the comparison.
            if (expected.EventLines.Count != actual.Count)
            {
                string actualHash = EventFingerprint.Of(actual);

                if (expected.EventFingerprint.Length > 0 &&
                    !string.Equals(expected.EventFingerprint, actualHash, StringComparison.Ordinal))
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.EventMismatch, expected.Sequence, checkedSoFar,
                        "events " + expected.EventFingerprint, "events " + actualHash,
                        expected.Command.Describe());
                }

                return null;
            }

            for (int index = 0; index < actual.Count; index++)
            {
                string line = EventFingerprint.Describe(actual[index]);

                if (!string.Equals(expected.EventLines[index], line, StringComparison.Ordinal))
                {
                    return ReplayVerificationResult.Diverged(
                        DivergenceKind.EventMismatch, expected.Sequence, checkedSoFar,
                        "event[" + index + "] " + expected.EventLines[index],
                        "event[" + index + "] " + line,
                        expected.Command.Describe());
                }
            }

            return null;
        }
    }
}
