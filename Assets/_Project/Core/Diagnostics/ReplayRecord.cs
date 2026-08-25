using System;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Setup;

namespace CoH.Core.Diagnostics
{
    /// <summary>Where a recorded match started from.</summary>
    public enum ReplayInitialSource
    {
        Match = 0,
        Scenario = 1
    }

    /// <summary>
    /// One submitted command and what the engine did with it.
    ///
    /// The events and the state hash are kept as the expected answer, never as
    /// the thing that rebuilds the match. Replaying re-executes the command and
    /// compares; it does not apply the recorded events to a state.
    /// </summary>
    public sealed class ReplayEntry
    {
        public ReplayEntry(
            int sequence,
            ReplayCommand command,
            bool accepted,
            RejectionReason reason,
            int eventCount,
            string eventFingerprint,
            string stateFingerprint,
            IReadOnlyList<string> eventLines)
        {
            Sequence = sequence;
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Accepted = accepted;
            Reason = reason;
            EventCount = eventCount;
            EventFingerprint = eventFingerprint ?? string.Empty;
            StateFingerprint = stateFingerprint ?? string.Empty;
            EventLines = eventLines ?? Array.Empty<string>();
        }

        /// <summary>Position in the recorded order, from zero. No timestamp is involved.</summary>
        public int Sequence { get; }

        public ReplayCommand Command { get; }

        /// <summary>
        /// Whether the engine took it.
        ///
        /// Refused commands are recorded too, and on purpose: "the engine
        /// refused something it should have accepted" is a bug, and a replay
        /// that quietly dropped it could never reproduce one.
        /// </summary>
        public bool Accepted { get; }

        public RejectionReason Reason { get; }

        public int EventCount { get; }

        public string EventFingerprint { get; }

        /// <summary>The state hash after this command. Empty when it was not captured.</summary>
        public string StateFingerprint { get; }

        /// <summary>
        /// The events themselves, one canonical line each.
        ///
        /// Kept in full because they are what a divergence report needs to be
        /// useful: knowing two hashes differ says nothing, and a match rarely
        /// produces enough events for the size to matter.
        /// </summary>
        public IReadOnlyList<string> EventLines { get; }

        public override string ToString() =>
            "#" + Sequence + " " + Command.Describe() + (Accepted ? " Accepted" : " Rejected(" + Reason + ")");
    }

    /// <summary>
    /// A mulligan that was carried out by the host rather than submitted by a
    /// player.
    ///
    /// The opening exchange is settled before anybody can click anything, so it
    /// never passes through the session and never becomes a recorded command.
    /// It still decides both opening hands, so a replay that did not carry it
    /// would start a fresh match still waiting for one and refuse the very
    /// first thing it was asked to do.
    /// </summary>
    public sealed class ReplayMulligan
    {
        public ReplayMulligan(PlayerId playerId, IReadOnlyList<EntityId> cardsToReplace)
        {
            PlayerId = playerId;

            CardsToReplace = cardsToReplace == null
                ? Array.Empty<EntityId>()
                : new List<EntityId>(cardsToReplace).ToArray();
        }

        public PlayerId PlayerId { get; }

        public IReadOnlyList<EntityId> CardsToReplace { get; }
    }

    /// <summary>
    /// Everything needed to play a match again from nothing.
    ///
    /// The point of the shape is that it holds inputs, not outcomes. A seed,
    /// the decks or the scenario it began from, and the ordered list of
    /// commands somebody actually submitted. Feed those to a brand new engine
    /// and the same match happens, which is what makes a replay a test of the
    /// engine rather than a recording of a screen.
    ///
    /// The recorded results are carried alongside as the expected answer. They
    /// are compared against, never applied.
    /// </summary>
    public sealed class ReplayRecord
    {
        private readonly List<ReplayEntry> _entries;

        public ReplayRecord(
            ReplayInitialSource initialSource,
            ulong seed,
            IReadOnlyList<CardId> deckOne,
            IReadOnlyList<CardId> deckTwo,
            string scenarioId,
            string catalogFingerprint,
            ReplayConfig config,
            IReadOnlyList<ReplayEntry> entries = null,
            int formatVersion = ReplayFormat.CurrentVersion,
            string createdAtUtc = "",
            IReadOnlyList<ReplayMulligan> mulliganChoices = null)
        {
            MulliganChoices = mulliganChoices ?? Array.Empty<ReplayMulligan>();
            FormatVersion = formatVersion;
            InitialSource = initialSource;
            Seed = seed;
            DeckOne = deckOne ?? Array.Empty<CardId>();
            DeckTwo = deckTwo ?? Array.Empty<CardId>();
            ScenarioId = scenarioId ?? string.Empty;
            CatalogFingerprint = catalogFingerprint ?? string.Empty;
            Config = config ?? ReplayConfig.From(GameConfig.Default);
            CreatedAtUtc = createdAtUtc ?? string.Empty;

            _entries = entries == null ? new List<ReplayEntry>() : new List<ReplayEntry>(entries);
        }

        public int FormatVersion { get; }

        public ReplayInitialSource InitialSource { get; }

        /// <summary>The seed a standard match was built from. Unused by a scenario replay.</summary>
        public ulong Seed { get; }

        public IReadOnlyList<CardId> DeckOne { get; }

        public IReadOnlyList<CardId> DeckTwo { get; }

        /// <summary>Which debug scenario this began from, when it did.</summary>
        public string ScenarioId { get; }

        /// <summary>What the cards did at the time. A different one makes the replay meaningless.</summary>
        public string CatalogFingerprint { get; }

        /// <summary>
        /// Mulligans the host settled before play began, applied again before
        /// the first recorded command. Empty for a scenario, which starts long
        /// after the opening hands.
        /// </summary>
        public IReadOnlyList<ReplayMulligan> MulliganChoices { get; }

        public ReplayConfig Config { get; }

        /// <summary>
        /// Only ever shown to a person, and never read by anything that
        /// verifies. The clock has no place in a deterministic replay.
        /// </summary>
        public string CreatedAtUtc { get; }

        public IReadOnlyList<ReplayEntry> Entries => _entries;

        public int CommandCount => _entries.Count;

        /// <summary>The state hash the recording finished on, or empty.</summary>
        public string FinalStateFingerprint =>
            _entries.Count == 0 ? string.Empty : _entries[_entries.Count - 1].StateFingerprint;

        internal void Add(ReplayEntry entry) => _entries.Add(entry);
    }

    /// <summary>
    /// The match constants a recording was played under.
    ///
    /// Recorded in full rather than fingerprinted: a replay that cannot run is
    /// much less useful than one that rebuilds the exact rules it was made
    /// with, and there are only eight numbers.
    /// </summary>
    public sealed class ReplayConfig
    {
        public ReplayConfig(
            int startingHeroHealth, int maxHandSize, int maxBoardSize, int maxManaCrystals,
            int deckSize, int startingPlayerHandSize, int secondPlayerHandSize, string secondPlayerExtraCard)
        {
            StartingHeroHealth = startingHeroHealth;
            MaxHandSize = maxHandSize;
            MaxBoardSize = maxBoardSize;
            MaxManaCrystals = maxManaCrystals;
            DeckSize = deckSize;
            StartingPlayerHandSize = startingPlayerHandSize;
            SecondPlayerHandSize = secondPlayerHandSize;
            SecondPlayerExtraCard = secondPlayerExtraCard ?? string.Empty;
        }

        public int StartingHeroHealth { get; }

        public int MaxHandSize { get; }

        public int MaxBoardSize { get; }

        public int MaxManaCrystals { get; }

        public int DeckSize { get; }

        public int StartingPlayerHandSize { get; }

        public int SecondPlayerHandSize { get; }

        public string SecondPlayerExtraCard { get; }

        public static ReplayConfig From(GameConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new ReplayConfig(
                config.StartingHeroHealth,
                config.MaxHandSize,
                config.MaxBoardSize,
                config.MaxManaCrystals,
                config.DeckSize,
                config.StartingPlayerHandSize,
                config.SecondPlayerHandSize,
                config.SecondPlayerExtraCard.IsNone ? string.Empty : config.SecondPlayerExtraCard.Value);
        }

        public GameConfig ToConfig() => new GameConfig(
            StartingHeroHealth,
            MaxHandSize,
            MaxBoardSize,
            MaxManaCrystals,
            DeckSize,
            StartingPlayerHandSize,
            SecondPlayerHandSize,
            string.IsNullOrEmpty(SecondPlayerExtraCard) ? default : new CardId(SecondPlayerExtraCard));
    }

    /// <summary>Version of the replay file format.</summary>
    public static class ReplayFormat
    {
        /// <summary>
        /// Bumped whenever a file written by an older build could be
        /// misunderstood by a newer one. A reader that meets a version it does
        /// not know refuses it by name rather than crashing on a missing field.
        /// </summary>
        public const int CurrentVersion = 1;

        public const string FileExtension = ".cohreplay.json";
    }
}
