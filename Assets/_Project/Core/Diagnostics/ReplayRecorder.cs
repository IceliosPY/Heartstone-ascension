using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// Builds a replay while a match is being played.
    ///
    /// Nothing pushes to it: something outside calls it after each command, and
    /// the engine has no idea it exists. That is deliberate. A recorder wired
    /// into the rules would be one more thing that could change what the rules
    /// do, and the whole value of a replay is that it did not.
    ///
    /// It only ever reads. It takes the command that was submitted, the result
    /// it got, and a look at the state afterwards, and turns them into values.
    /// </summary>
    public sealed class ReplayRecorder
    {
        private readonly ReplayRecord _record;
        private int _next;

        private ReplayRecorder(ReplayRecord record)
        {
            _record = record;
        }

        /// <summary>Starts recording a standard match.</summary>
        public static ReplayRecorder ForMatch(
            ulong seed, DeckList deckOne, DeckList deckTwo, ICardCatalog catalog, GameConfig config,
            string createdAtUtc = "", IReadOnlyList<ReplayMulligan> mulliganChoices = null)
        {
            if (deckOne == null)
            {
                throw new ArgumentNullException(nameof(deckOne));
            }

            if (deckTwo == null)
            {
                throw new ArgumentNullException(nameof(deckTwo));
            }

            return new ReplayRecorder(new ReplayRecord(
                ReplayInitialSource.Match,
                seed,
                deckOne.Cards,
                deckTwo.Cards,
                scenarioId: string.Empty,
                catalogFingerprint: CatalogFingerprint.Of(catalog),
                config: ReplayConfig.From(config ?? GameConfig.Default),
                createdAtUtc: createdAtUtc,
                mulliganChoices: mulliganChoices));
        }

        /// <summary>Starts recording a match that began from a prepared situation.</summary>
        public static ReplayRecorder ForScenario(
            string scenarioId, ICardCatalog catalog, GameConfig config, string createdAtUtc = "")
        {
            if (string.IsNullOrEmpty(scenarioId))
            {
                throw new ArgumentException("A scenario replay needs the scenario id.", nameof(scenarioId));
            }

            return new ReplayRecorder(new ReplayRecord(
                ReplayInitialSource.Scenario,
                seed: 0UL,
                deckOne: Array.Empty<CardId>(),
                deckTwo: Array.Empty<CardId>(),
                scenarioId: scenarioId,
                catalogFingerprint: CatalogFingerprint.Of(catalog),
                config: ReplayConfig.From(config ?? GameConfig.Default),
                createdAtUtc: createdAtUtc));
        }

        public ReplayRecord Record => _record;

        public int CommandCount => _record.CommandCount;

        /// <summary>
        /// Notes one submitted command, whatever the engine made of it.
        ///
        /// The state is read after the fact, so a refused command records the
        /// state it did not change, which is exactly what a verification wants
        /// to compare against.
        /// </summary>
        public ReplayEntry Observe(GameCommand command, CommandResult result, GameState state)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string[] lines = new string[result.Events.Count];

            for (int index = 0; index < result.Events.Count; index++)
            {
                lines[index] = EventFingerprint.Describe(result.Events[index]);
            }

            ReplayEntry entry = new ReplayEntry(
                _next++,
                ReplayCommand.From(command),
                result.IsAccepted,
                result.Reason,
                result.Events.Count,
                EventFingerprint.Of(result.Events),
                state == null ? string.Empty : StateFingerprint.Of(state),
                lines);

            _record.Add(entry);
            return entry;
        }
    }
}
