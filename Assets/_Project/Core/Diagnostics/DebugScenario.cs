using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>A minion already standing on a board when a scenario begins.</summary>
    public sealed class ScenarioMinion
    {
        public ScenarioMinion(
            string cardId,
            int damage = 0,
            int attacksThisTurn = 0,
            int summonedOnTurn = 1,
            int attackModifier = 0,
            int healthModifier = 0)
        {
            CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
            Damage = damage;
            AttacksThisTurn = attacksThisTurn;
            SummonedOnTurn = summonedOnTurn;
            AttackModifier = attackModifier;
            HealthModifier = healthModifier;
        }

        public string CardId { get; }

        /// <summary>
        /// Damage already taken. How a scenario makes a two for three into a
        /// two for one without inventing a card to do it with.
        /// </summary>
        public int Damage { get; }

        public int AttacksThisTurn { get; }

        /// <summary>
        /// Turn it arrived on. Below the scenario's turn number it is free to
        /// act; equal or above and it is summoning sick, by the same rule the
        /// engine always applies.
        /// </summary>
        public int SummonedOnTurn { get; }

        public int AttackModifier { get; }

        public int HealthModifier { get; }
    }

    /// <summary>One side of a prepared situation.</summary>
    public sealed class ScenarioPlayer
    {
        public ScenarioPlayer(
            int heroHealth = 30,
            int armor = 0,
            int maxMana = 10,
            int availableMana = 10,
            int fatigueCounter = 0,
            IReadOnlyList<string> hand = null,
            IReadOnlyList<string> deck = null,
            IReadOnlyList<ScenarioMinion> board = null)
        {
            HeroHealth = heroHealth;
            Armor = armor;
            MaxMana = maxMana;
            AvailableMana = availableMana;
            FatigueCounter = fatigueCounter;
            Hand = hand ?? Array.Empty<string>();
            Deck = deck ?? Array.Empty<string>();
            Board = board ?? Array.Empty<ScenarioMinion>();
        }

        public int HeroHealth { get; }

        public int Armor { get; }

        public int MaxMana { get; }

        public int AvailableMana { get; }

        public int FatigueCounter { get; }

        /// <summary>Cards in hand, left to right.</summary>
        public IReadOnlyList<string> Hand { get; }

        /// <summary>Cards in the deck, top first.</summary>
        public IReadOnlyList<string> Deck { get; }

        /// <summary>Minions in play, left to right.</summary>
        public IReadOnlyList<ScenarioMinion> Board { get; }
    }

    /// <summary>
    /// A position to start from, written down as data.
    ///
    /// Data rather than a sequence of pokes at a live match, because a scenario
    /// has to build the same thing every time, be readable at a glance, and be
    /// usable from a test as easily as from a button. It says what the position
    /// is, and <see cref="DebugScenarioBuilder"/> is the only thing that knows
    /// how to make one.
    ///
    /// It changes no rule. Everything it can express is a value the engine
    /// already holds during a normal match; there is no way to write down a
    /// position the rules could not have produced by being played.
    /// </summary>
    public sealed class DebugScenario
    {
        public DebugScenario(
            string id,
            string description,
            ScenarioPlayer one,
            ScenarioPlayer two,
            int turnNumber = 5,
            PlayerId activePlayer = default,
            GamePhase phase = GamePhase.Playing)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("A scenario needs an id.", nameof(id));
            }

            Id = id;
            Description = description ?? string.Empty;
            One = one ?? throw new ArgumentNullException(nameof(one));
            Two = two ?? throw new ArgumentNullException(nameof(two));
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer.IsNone ? PlayerId.One : activePlayer;
            Phase = phase;
        }

        /// <summary>Stable name. Written into a replay that began here.</summary>
        public string Id { get; }

        public string Description { get; }

        public ScenarioPlayer One { get; }

        public ScenarioPlayer Two { get; }

        public int TurnNumber { get; }

        public PlayerId ActivePlayer { get; }

        public GamePhase Phase { get; }

        public override string ToString() => Id;
    }
}
