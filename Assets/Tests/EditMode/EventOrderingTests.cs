using System.Collections.Generic;
using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The event stream is the contract between the engine and the presentation
    /// layer: Unity replays it as an animation sequence. Its order therefore has
    /// to be part of the engine's behaviour, not an accident of implementation.
    /// </summary>
    public sealed class EventOrderingTests
    {
        private static List<string> TypeNames(IEnumerable<GameEvent> events) =>
            events.Select(gameEvent => gameEvent.GetType().Name).ToList();

        private static List<string> Descriptions(IEnumerable<GameEvent> events) =>
            events.Select(gameEvent => gameEvent.ToString()).ToList();

        [Test]
        public void Setup_reports_the_match_start_then_the_deal_then_the_mulligan()
        {
            GameEngine engine = TestFactory.Engine(seed: 8UL);
            List<string> events = TypeNames(engine.StartMatch(TestFactory.Deck(), TestFactory.Deck()));

            Assert.That(events.First(), Is.EqualTo(nameof(GameStartedEvent)));
            Assert.That(events.Last(), Is.EqualTo(nameof(MulliganStartedEvent)));
            Assert.That(events.Count(name => name == nameof(CardDrawnEvent)), Is.EqualTo(7),
                "Three cards for the starting player, four for the other.");
        }

        [Test]
        public void A_turn_starts_with_the_banner_then_mana_then_the_draw()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CommandResult result = TestFactory.EndTurn(engine);

            List<string> events = TypeNames(result.Events);

            Assert.That(events, Is.EqualTo(new List<string>
            {
                nameof(TurnEndedEvent),
                nameof(TurnStartedEvent),
                nameof(ManaCrystalGainedEvent),
                nameof(ManaRefilledEvent),
                nameof(CardDrawnEvent)
            }));
        }

        [Test]
        public void A_turn_at_the_mana_cap_reports_no_crystal_gain()
        {
            GameEngine engine = TestFactory.StartedMatch();

            for (int turn = 0; turn < 24; turn++)
            {
                TestFactory.EndTurn(engine);
            }

            CommandResult result = TestFactory.EndTurn(engine);
            List<string> events = TypeNames(result.Events);

            Assert.That(events.Contains(nameof(ManaCrystalGainedEvent)), Is.False);
            Assert.That(events.Contains(nameof(ManaRefilledEvent)), Is.True, "Mana is still refilled.");
        }

        [Test]
        public void Fatigue_reports_its_cause_then_its_effect()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            TestFactory.EmptyDeck(engine.State.GetPlayer(starting));

            TestFactory.EndTurn(engine);
            CommandResult result = TestFactory.EndTurn(engine);

            List<string> events = TypeNames(result.Events);
            int fatigue = events.IndexOf(nameof(FatigueDamageEvent));
            int damage = events.IndexOf(nameof(DamageDealtEvent));

            Assert.That(fatigue, Is.GreaterThanOrEqualTo(0));
            Assert.That(damage, Is.EqualTo(fatigue + 1), "The damage follows the fatigue that caused it.");
            Assert.That(events.Contains(nameof(CardDrawnEvent)), Is.False, "Nothing was drawn.");
        }

        [Test]
        public void The_end_of_the_match_is_the_last_thing_reported()
        {
            GameEngine engine = TestFactory.StartedMatch(
                config: new CoH.Core.Setup.GameConfig(startingHeroHealth: 3));
            PlayerId starting = engine.State.StartingPlayer;
            TestFactory.EmptyDeck(engine.State.GetPlayer(starting));

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);
            CommandResult result = TestFactory.EndTurn(engine);

            List<string> events = TypeNames(result.Events);
            Assert.That(events.Last(), Is.EqualTo(nameof(GameEndedEvent)));
        }

        [Test]
        public void The_mulligan_resolution_reports_both_players_then_the_extra_card()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 3UL);
            engine.Execute(new MulliganCommand(engine.State.StartingPlayer));
            CommandResult result = engine.Execute(new MulliganCommand(engine.State.StartingPlayer.Opponent));

            List<string> events = TypeNames(result.Events);

            Assert.That(events.Count(name => name == nameof(MulliganResolvedEvent)), Is.EqualTo(2));
            Assert.That(
                events.IndexOf(nameof(CardGeneratedEvent)),
                Is.GreaterThan(events.LastIndexOf(nameof(MulliganResolvedEvent))),
                "The extra card comes after both mulligans are done.");
            Assert.That(
                events.IndexOf(nameof(TurnStartedEvent)),
                Is.GreaterThan(events.IndexOf(nameof(CardGeneratedEvent))),
                "And the first turn comes after that.");
        }

        [Test]
        public void A_refused_command_reports_nothing_at_all()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId idle = engine.State.StartingPlayer.Opponent;

            CommandResult result = engine.Execute(new EndTurnCommand(idle));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void The_same_seed_and_the_same_commands_replay_identically()
        {
            List<string> left = RunScriptedMatch(seed: 4321UL);
            List<string> right = RunScriptedMatch(seed: 4321UL);

            Assert.That(right, Is.EqualTo(left));
        }

        [Test]
        public void A_different_seed_produces_a_different_stream()
        {
            List<string> left = RunScriptedMatch(seed: 4321UL);
            List<string> right = RunScriptedMatch(seed: 9999UL);

            Assert.That(right, Is.Not.EqualTo(left));
        }

        [Test]
        public void The_same_seed_and_the_same_commands_reach_the_same_state()
        {
            GameEngine left = TestFactory.StartedMatch(seed: 271UL);
            GameEngine right = TestFactory.StartedMatch(seed: 271UL);

            for (int turn = 0; turn < 12; turn++)
            {
                TestFactory.EndTurn(left);
                TestFactory.EndTurn(right);
            }

            Assert.That(right.State.TurnNumber, Is.EqualTo(left.State.TurnNumber));
            Assert.That(right.State.CurrentPlayer, Is.EqualTo(left.State.CurrentPlayer));

            foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
            {
                Player leftPlayer = left.State.GetPlayer(seat);
                Player rightPlayer = right.State.GetPlayer(seat);

                Assert.That(rightPlayer.MaxMana, Is.EqualTo(leftPlayer.MaxMana));
                Assert.That(rightPlayer.FatigueCounter, Is.EqualTo(leftPlayer.FatigueCounter));
                Assert.That(rightPlayer.Hero.CurrentHealth, Is.EqualTo(leftPlayer.Hero.CurrentHealth));
                Assert.That(
                    rightPlayer.Hand.Select(card => card.Id.Value),
                    Is.EqualTo(leftPlayer.Hand.Select(card => card.Id.Value)));
                Assert.That(
                    rightPlayer.Deck.Select(card => card.Id.Value),
                    Is.EqualTo(leftPlayer.Deck.Select(card => card.Id.Value)));
            }
        }

        /// <summary>
        /// Plays a fixed script: set up, both players mulligan their first card,
        /// then eight turns are passed. Returns the whole event stream as text.
        /// </summary>
        private static List<string> RunScriptedMatch(ulong seed)
        {
            GameEngine engine = TestFactory.Engine(seed);
            List<GameEvent> stream = new List<GameEvent>(
                engine.StartMatch(TestFactory.Deck(), TestFactory.Deck()));

            foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
            {
                EntityId firstCard = engine.State.GetPlayer(seat).Hand[0].Id;
                stream.AddRange(engine.Execute(new MulliganCommand(seat, firstCard)).Events);
            }

            for (int turn = 0; turn < 8; turn++)
            {
                stream.AddRange(TestFactory.EndTurn(engine).Events);
            }

            return Descriptions(stream);
        }
    }
}
