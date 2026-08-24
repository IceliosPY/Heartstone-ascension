using System;
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
            Graveyard = new Zone<CardInstance>(ZoneType.Graveyard);
        }

        public PlayerId Id { get; }

        public Hero Hero { get; }

        /// <summary>Draw pile. Index 0 is the next card drawn.</summary>
        public Zone<CardInstance> Deck { get; }

        public Zone<CardInstance> Hand { get; }

        /// <summary>Minions in play, left to right. The index is the board position.</summary>
        public Zone<Minion> Board { get; }

        public Zone<CardInstance> Graveyard { get; }

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

        public override string ToString() => "Player " + Id;
    }
}
