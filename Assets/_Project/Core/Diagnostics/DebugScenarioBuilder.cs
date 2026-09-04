using System;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// Turns a written position into a match the rules will accept.
    ///
    /// This is the one place allowed to put a match somewhere it did not play
    /// its way to, and it lives inside the engine assembly because everything
    /// that makes a state coherent, zones, controllers, timestamps, is internal
    /// and meant to stay that way.
    ///
    /// It is strict on purpose. Every minion gets its zone, its controller, its
    /// timestamp and its place in the row, exactly as a summon would have given
    /// them, and an unknown card id is refused rather than skipped. A scenario
    /// that produced a slightly broken state would spend its life manufacturing
    /// bugs that do not exist, which is worse than not having scenarios at all.
    ///
    /// Everything is built in a written order, so the same scenario always
    /// produces the same entity ids: both heroes first, from the state
    /// constructor, then seat one's deck, hand and board, then seat two's.
    /// Tests can therefore name an entity before the match has run.
    /// </summary>
    public static class DebugScenarioBuilder
    {
        /// <summary>Builds the position. Never used to start a real match.</summary>
        public static GameState Build(DebugScenario scenario, ICardCatalog catalog, GameConfig config = null)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            GameConfig rules = config ?? GameConfig.Default;

            // The seed is fixed rather than absent: a scenario has to build the
            // same thing twice, and anything that draws from the source later
            // has to draw the same value.
            GameState state = new GameState(rules, catalog, seed: 0UL);

            Fill(state, PlayerId.One, scenario.One, scenario);
            Fill(state, PlayerId.Two, scenario.Two, scenario);
            AssignHeroPowers(state);

            state.TurnNumber = scenario.TurnNumber;
            state.Phase = scenario.Phase;
            state.CurrentPlayer = scenario.Phase == GamePhase.Playing ? scenario.ActivePlayer : PlayerId.None;
            state.StartingPlayer = scenario.ActivePlayer;
            state.Result = GameResult.InProgress;

            return state;
        }

        /// <summary>Builds the position and an engine ready to take commands for it.</summary>
        public static GameEngineHandle Start(
            DebugScenario scenario, ICardCatalog catalog, GameConfig config = null)
        {
            GameState state = Build(scenario, catalog, config);
            return new GameEngineHandle(Rules.GameEngine.FromState(state), state);
        }

        private static void Fill(
            GameState state, PlayerId seat, ScenarioPlayer described, DebugScenario scenario)
        {
            Player player = state.GetPlayer(seat);

            player.Hero.Damage = Math.Max(0, player.Hero.MaxHealth - described.HeroHealth);
            player.Hero.Armor = described.Armor;
            player.Hero.AttacksThisTurn = 0;

            player.MaxMana = described.MaxMana;
            player.AvailableMana = described.AvailableMana;
            player.FatigueCounter = described.FatigueCounter;

            // Both mulligans are behind us: a scenario starts in the middle of a
            // match, not at its beginning.
            player.HasConfirmedMulligan = true;
            player.TurnsTaken = Math.Max(1, scenario.TurnNumber / 2);

            for (int index = 0; index < described.Deck.Count; index++)
            {
                CardInstance card = CreateCard(state, described.Deck[index], seat);
                card.Zone = ZoneType.Deck;
                player.Deck.TryAdd(card);
            }

            for (int index = 0; index < described.Hand.Count; index++)
            {
                CardInstance card = CreateCard(state, described.Hand[index], seat);
                card.Zone = ZoneType.Hand;

                if (!player.Hand.TryAdd(card))
                {
                    throw new InvalidOperationException(
                        "Scenario '" + scenario.Id + "' puts more cards in a hand than " +
                        player.Hand.Capacity + " will hold.");
                }
            }

            for (int index = 0; index < described.Board.Count; index++)
            {
                ScenarioMinion described_minion = described.Board[index];
                Minion minion = CreateMinion(state, described_minion.CardId, seat);

                minion.Zone = ZoneType.Play;
                minion.Damage = described_minion.Damage;
                minion.AttacksThisTurn = described_minion.AttacksThisTurn;
                minion.SummonedOnTurn = described_minion.SummonedOnTurn;
                if (described_minion.AttackModifier != 0 || described_minion.HealthModifier != 0)
                {
                    minion.AddModifier(described_minion.AttackModifier, described_minion.HealthModifier);
                }

                // Handed out here for the same reason a summon hands one out:
                // order of entry decides who dies first when two die together.
                minion.Timestamp = state.NextTimestamp();

                if (!player.Board.TryAdd(minion))
                {
                    throw new InvalidOperationException(
                        "Scenario '" + scenario.Id + "' puts more minions on a board than " +
                        player.Board.Capacity + " will hold.");
                }
            }
        }

        /// <summary>
        /// Gives each seat the hero power its config brings, mirroring
        /// <c>MatchSetup</c>'s own setup step. A scenario built without this
        /// would drop a player into a match its own bootstrap says it should
        /// have a hero power for, and then have none - so every scenario, not
        /// only ones written with a hero power in mind, needs this to reflect
        /// the match it was cut from.
        /// </summary>
        private static void AssignHeroPowers(GameState state)
        {
            for (int index = 0; index < state.Players.Count; index++)
            {
                Player player = state.Players[index];
                CardId heroPower = state.Config.HeroPowerFor(player.Id);

                if (heroPower.IsNone)
                {
                    continue;
                }

                if (!state.Catalog.TryGet(heroPower, out CardDefinition definition))
                {
                    throw new InvalidOperationException(
                        "The catalog has no definition for " + player.Id + "'s hero power: " + heroPower);
                }

                if (definition.Type != CardType.HeroPower)
                {
                    throw new InvalidOperationException(
                        player.Id + "'s hero power " + heroPower + " is a " + definition.Type +
                        ", not a hero power.");
                }

                player.Hero.HeroPowerCardId = heroPower;
            }
        }

        private static CardInstance CreateCard(GameState state, string cardId, PlayerId owner)
        {
            RequireKnown(state, cardId);
            return state.CreateCardInstance(new CardId(cardId), owner);
        }

        private static Minion CreateMinion(GameState state, string cardId, PlayerId owner)
        {
            CardDefinition definition = RequireKnown(state, cardId);

            if (definition.Type != CardType.Minion)
            {
                throw new InvalidOperationException(
                    "A scenario put '" + cardId + "' on a board, but it is a " + definition.Type + ".");
            }

            return state.CreateMinion(definition.Id, owner);
        }

        private static CardDefinition RequireKnown(GameState state, string cardId)
        {
            if (!state.Catalog.TryGet(new CardId(cardId), out CardDefinition definition))
            {
                throw new InvalidOperationException(
                    "A scenario names a card the catalog does not have: '" + cardId + "'.");
            }

            return definition;
        }
    }

    /// <summary>An engine and the state it was handed, returned together.</summary>
    public readonly struct GameEngineHandle
    {
        public GameEngineHandle(Rules.GameEngine engine, GameState state)
        {
            Engine = engine;
            State = state;
        }

        public Rules.GameEngine Engine { get; }

        public GameState State { get; }
    }
}
