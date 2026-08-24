using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Core.Setup;

namespace CoH.Core.State
{
    /// <summary>
    /// One side of a match: a hero, four zones, and the per-player counters the
    /// rules will read and write.
    ///
    /// The counters exist now but nothing drives them yet; gaining mana,
    /// drawing and taking fatigue are rules and arrive in a later phase.
    /// </summary>
    public sealed class Player
    {
        internal Player(PlayerId id, Hero hero, GameConfig config)
        {
            if (id.IsNone)
            {
                throw new ArgumentException("A player needs a real id.", nameof(id));
            }

            Id = id;
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));

            Deck = new Zone<CardInstance>(ZoneType.Deck);
            Hand = new Zone<CardInstance>(ZoneType.Hand, config.MaxHandSize);
            Board = new Zone<Minion>(ZoneType.Play, config.MaxBoardSize);
            Graveyard = new Zone<Entity>(ZoneType.Graveyard);

            _mulliganSelection = new List<EntityId>();
        }

        private readonly List<EntityId> _mulliganSelection;

        public PlayerId Id { get; }

        public Hero Hero { get; }

        /// <summary>Draw pile. Index 0 is the next card drawn.</summary>
        public Zone<CardInstance> Deck { get; }

        public Zone<CardInstance> Hand { get; }

        /// <summary>Minions in play, left to right. The index is the board position.</summary>
        public Zone<Minion> Board { get; }

        /// <summary>
        /// Everything of this player's that has died or been destroyed, in the
        /// order it happened: burned cards, spent cards later, and the minions
        /// removed by death phases.
        ///
        /// Typed as Entity rather than CardInstance because a dead minion is
        /// not a card: it is a distinct entity that was in play. Keeping one
        /// graveyard rather than two preserves the real order of events, which
        /// resurrection effects will need.
        /// </summary>
        public Zone<Entity> Graveyard { get; }

        /// <summary>Mana crystals owned, up to GameConfig.MaxManaCrystals.</summary>
        public int MaxMana { get; internal set; }

        /// <summary>Mana left to spend this turn.</summary>
        public int AvailableMana { get; internal set; }

        /// <summary>
        /// Mana granted for this turn only, on top of the crystals owned. The
        /// Coin is the obvious case, which is why available mana cannot simply
        /// be derived from <see cref="MaxMana"/>.
        /// </summary>
        public int TemporaryMana { get; internal set; }

        /// <summary>Crystals that will be locked at the start of the next turn.</summary>
        public int OverloadOwed { get; internal set; }

        /// <summary>Crystals locked for the current turn.</summary>
        public int OverloadLocked { get; internal set; }

        /// <summary>
        /// How much fatigue damage the next empty-deck draw will deal. It grows
        /// by one each time and never resets.
        /// </summary>
        public int FatigueCounter { get; internal set; }

        public bool HasUsedHeroPowerThisTurn { get; internal set; }

        /// <summary>
        /// How many turns this player has begun.
        ///
        /// Distinct from GameState.TurnNumber, which counts turns across the
        /// whole match. Keeping the two apart avoids the classic bug where one
        /// system reads a turn counter as "the match's third turn" and another
        /// as "this player's third turn".
        /// </summary>
        public int TurnsTaken { get; internal set; }

        /// <summary>Whether this player has already submitted their mulligan choice.</summary>
        public bool HasConfirmedMulligan { get; internal set; }

        /// <summary>
        /// Cards this player asked to replace, kept between their confirmation
        /// and the moment both players' mulligans are resolved together.
        /// Empty outside the mulligan phase.
        /// </summary>
        public IReadOnlyList<EntityId> MulliganSelection => _mulliganSelection;

        internal void SetMulliganSelection(IEnumerable<EntityId> selection)
        {
            _mulliganSelection.Clear();
            _mulliganSelection.AddRange(selection);
        }

        internal void ClearMulliganSelection() => _mulliganSelection.Clear();

        public override string ToString() => "Player " + Id;
    }
}
