using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Prepared positions: they build the same thing every time, and each one
    /// actually does what its name promises.
    ///
    /// The second half is the one that matters. A scenario called double death
    /// that quietly stopped producing a double death would waste an afternoon
    /// before anybody suspected the scenario rather than the code being tested
    /// with it, so each of these plays the situation out and checks the ending.
    /// </summary>
    public sealed class DebugScenarioTests
    {
        private static CardCatalog Catalog() => TestFactory.Catalog(
            TestFactory.MinionDefinition(manaCost: 2, attack: 2, health: 3),
            TestFactory.CoinDefinition());

        private static GameState Build(DebugScenario scenario) =>
            DebugScenarioBuilder.Build(scenario, Catalog(), GameConfig.Default);

        private static GameEngine Start(DebugScenario scenario) =>
            DebugScenarioBuilder.Start(scenario, Catalog(), GameConfig.Default).Engine;

        // ------------------------------------------------------------------

        [Test]
        public void Building_the_same_scenario_twice_gives_the_same_state()
        {
            foreach (DebugScenario scenario in DebugScenarios.All)
            {
                GameState first = Build(scenario);
                GameState second = Build(scenario);

                Assert.That(
                    StateFingerprint.Of(second), Is.EqualTo(StateFingerprint.Of(first)),
                    "Scenario '" + scenario.Id + "' did not build the same position twice.");
            }
        }

        /// <summary>
        /// Ids have to be the same every time, or a test written against a
        /// scenario could not name the minion it is about.
        /// </summary>
        [Test]
        public void Entity_ids_are_the_same_every_time_a_scenario_is_built()
        {
            foreach (DebugScenario scenario in DebugScenarios.All)
            {
                GameState first = Build(scenario);
                GameState second = Build(scenario);

                foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
                {
                    Player left = first.GetPlayer(seat);
                    Player right = second.GetPlayer(seat);

                    Assert.That(right.Hero.Id, Is.EqualTo(left.Hero.Id), scenario.Id);

                    for (int index = 0; index < left.Board.Count; index++)
                    {
                        Assert.That(right.Board[index].Id, Is.EqualTo(left.Board[index].Id),
                            "Scenario '" + scenario.Id + "' gave a board minion a different id.");
                    }

                    for (int index = 0; index < left.Hand.Count; index++)
                    {
                        Assert.That(right.Hand[index].Id, Is.EqualTo(left.Hand[index].Id),
                            "Scenario '" + scenario.Id + "' gave a hand card a different id.");
                    }
                }
            }
        }

        /// <summary>
        /// Every position must be one the rules could have produced. A scenario
        /// that skipped a zone or a controller would manufacture bugs that do
        /// not exist.
        /// </summary>
        [Test]
        public void Every_scenario_builds_a_coherent_position()
        {
            foreach (DebugScenario scenario in DebugScenarios.All)
            {
                GameState state = Build(scenario);

                Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing), scenario.Id);
                Assert.That(state.CurrentPlayer.IsNone, Is.False, scenario.Id);
                Assert.That(state.TurnNumber, Is.GreaterThan(0), scenario.Id);
                Assert.That(state.HasEnded, Is.False, scenario.Id);

                foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
                {
                    Player player = state.GetPlayer(seat);

                    Assert.That(player.HasConfirmedMulligan, Is.True,
                        "Scenario '" + scenario.Id + "' starts mid match, so the mulligan is behind it.");

                    for (int index = 0; index < player.Board.Count; index++)
                    {
                        Minion minion = player.Board[index];

                        Assert.That(minion.Zone, Is.EqualTo(ZoneType.Play), scenario.Id);
                        Assert.That(minion.Owner, Is.EqualTo(seat), scenario.Id);
                        Assert.That(minion.Controller, Is.EqualTo(seat), scenario.Id);
                        Assert.That(minion.Timestamp, Is.GreaterThan(0),
                            "Scenario '" + scenario.Id + "' left a minion with no order of entry.");
                        Assert.That(minion.IsInPlay, Is.True, scenario.Id);
                    }

                    for (int index = 0; index < player.Hand.Count; index++)
                    {
                        Assert.That(player.Hand[index].Zone, Is.EqualTo(ZoneType.Hand), scenario.Id);
                        Assert.That(player.Hand[index].Owner, Is.EqualTo(seat), scenario.Id);
                    }

                    for (int index = 0; index < player.Deck.Count; index++)
                    {
                        Assert.That(player.Deck[index].Zone, Is.EqualTo(ZoneType.Deck), scenario.Id);
                    }
                }
            }
        }

        /// <summary>Board order is state, so it has to come out in the order it was written.</summary>
        [Test]
        public void Boards_are_built_in_the_order_they_were_described()
        {
            GameState state = Build(DebugScenarios.SevenMinionBoard);
            Player player = state.GetPlayer(PlayerId.One);

            Assert.That(player.Board.Count, Is.EqualTo(7));

            for (int index = 1; index < player.Board.Count; index++)
            {
                Assert.That(
                    player.Board[index].Id.Value,
                    Is.GreaterThan(player.Board[index - 1].Id.Value),
                    "Minions were not created left to right.");

                Assert.That(
                    player.Board[index].Timestamp,
                    Is.GreaterThan(player.Board[index - 1].Timestamp),
                    "Order of entry has to follow board order for a freshly built row.");
            }
        }

        // ------------------------------------------------------------------
        //  Each scenario does what it says
        // ------------------------------------------------------------------

        [Test]
        public void Ready_combat_gives_the_active_player_a_minion_that_can_attack()
        {
            GameEngine engine = Start(DebugScenarios.ReadyCombat);
            GameState state = engine.State;

            Assert.That(state.CurrentPlayer, Is.EqualTo(PlayerId.One));
            Assert.That(state.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.Two).Board.Count, Is.EqualTo(1));

            EntityId attacker = state.GetPlayer(PlayerId.One).Board[0].Id;

            Assert.That(engine.CanAttack(PlayerId.One, attacker), Is.EqualTo(RejectionReason.None),
                "The whole point of this scenario is being able to attack immediately.");
            Assert.That(engine.GetLegalAttackTargets(PlayerId.One, attacker), Is.Not.Empty);
        }

        [Test]
        public void Both_survive_really_does_leave_both_minions_standing()
        {
            GameEngine engine = Start(DebugScenarios.BothSurvive);
            GameState state = engine.State;

            EntityId attacker = state.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId defender = state.GetPlayer(PlayerId.Two).Board[0].Id;

            CommandResult result = engine.Execute(new AttackCommand(PlayerId.One, attacker, defender));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(state.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.Two).Board.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.One).Board[0].CurrentHealth, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.Two).Board[0].CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void Double_death_really_does_kill_both_minions_at_once()
        {
            GameEngine engine = Start(DebugScenarios.DoubleDeath);
            GameState state = engine.State;

            EntityId attacker = state.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId defender = state.GetPlayer(PlayerId.Two).Board[0].Id;

            CommandResult result = engine.Execute(new AttackCommand(PlayerId.One, attacker, defender));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(state.GetPlayer(PlayerId.One).Board.Count, Is.Zero, "The attacker should have died.");
            Assert.That(state.GetPlayer(PlayerId.Two).Board.Count, Is.Zero, "The defender should have died.");

            int deaths = 0;

            foreach (var reported in result.Events)
            {
                if (reported is CoH.Core.Events.MinionDiedEvent)
                {
                    deaths++;
                }
            }

            Assert.That(deaths, Is.EqualTo(2), "Both deaths should have been reported.");
        }

        [Test]
        public void Hero_lethal_really_does_end_the_match_in_one_attack()
        {
            GameEngine engine = Start(DebugScenarios.HeroLethal);
            GameState state = engine.State;

            EntityId attacker = state.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId enemyHero = state.GetPlayer(PlayerId.Two).Hero.Id;

            Assert.That(state.GetPlayer(PlayerId.Two).Hero.CurrentHealth, Is.EqualTo(2));

            CommandResult result = engine.Execute(new AttackCommand(PlayerId.One, attacker, enemyHero));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(state.HasEnded, Is.True, "Two damage into two health should have finished it.");
            Assert.That(state.Result, Is.EqualTo(GameResult.PlayerOneWins));
        }

        [Test]
        public void Full_hand_holds_exactly_ten_cards_with_a_deck_left()
        {
            GameState state = Build(DebugScenarios.FullHand);
            Player player = state.GetPlayer(PlayerId.One);

            Assert.That(player.Hand.Count, Is.EqualTo(10));
            Assert.That(player.Hand.IsFull, Is.True);
            Assert.That(player.Deck.Count, Is.GreaterThan(0),
                "A full hand only burns if there is still something to draw.");
        }

        [Test]
        public void Fatigue_starts_with_an_empty_deck_and_hurts_on_the_next_draw()
        {
            GameEngine engine = Start(DebugScenarios.Fatigue);
            GameState state = engine.State;

            Assert.That(state.GetPlayer(PlayerId.One).Deck.Count, Is.Zero);

            int before = state.GetPlayer(PlayerId.One).Hero.CurrentHealth;

            // Round trip: their next turn draws from nothing.
            engine.Execute(new EndTurnCommand(PlayerId.One));
            engine.Execute(new EndTurnCommand(PlayerId.Two));

            Assert.That(state.CurrentPlayer, Is.EqualTo(PlayerId.One));
            Assert.That(state.GetPlayer(PlayerId.One).Hero.CurrentHealth, Is.LessThan(before),
                "Drawing from an empty deck should have hurt.");
            Assert.That(state.GetPlayer(PlayerId.One).FatigueCounter, Is.GreaterThan(0));
        }

        [Test]
        public void Seven_minion_board_leaves_no_room_for_another()
        {
            GameEngine engine = Start(DebugScenarios.SevenMinionBoard);
            GameState state = engine.State;
            Player player = state.GetPlayer(PlayerId.One);

            Assert.That(player.Board.Count, Is.EqualTo(7));
            Assert.That(player.Board.IsFull, Is.True);
            Assert.That(player.Hand.Count, Is.GreaterThan(0));

            Assert.That(
                engine.CanExecute(new PlayCardCommand(PlayerId.One, player.Hand[0].Id)),
                Is.EqualTo(RejectionReason.BoardFull));
        }

        // ------------------------------------------------------------------

        [Test]
        public void A_scenario_naming_an_unknown_card_is_refused_clearly()
        {
            DebugScenario broken = new DebugScenario(
                "broken", "Names a card nobody has.",
                one: new ScenarioPlayer(hand: new[] { "no_such_card" }),
                two: new ScenarioPlayer());

            System.InvalidOperationException error =
                Assert.Throws<System.InvalidOperationException>(() => Build(broken));

            Assert.That(error.Message, Does.Contain("no_such_card"));
        }

        [Test]
        public void Every_scenario_can_be_found_by_its_id()
        {
            foreach (DebugScenario scenario in DebugScenarios.All)
            {
                Assert.That(DebugScenarios.TryFind(scenario.Id, out DebugScenario found), Is.True);
                Assert.That(found.Id, Is.EqualTo(scenario.Id));
                Assert.That(found.Description, Is.Not.Empty, scenario.Id + " has no description.");
            }

            Assert.That(DebugScenarios.TryFind("not_a_scenario", out DebugScenario _), Is.False);
        }
    }
}
