using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The Necromancer's hero power: what it costs, when it may be used, and
    /// what choosing one of its four options actually does.
    ///
    /// The mechanism under test is deliberately not "the Necromancer". It is a
    /// hero power that offers a fixed, ordered list of options and resolves
    /// exactly one of them, and the Necromancer is the first card to use it.
    /// Nothing in the engine names a servant or counts to four; these tests
    /// read the option list out of the card and assert against that, so a
    /// second class with three options would need no change here.
    /// </summary>
    public sealed class NecromancerHeroPowerTests
    {
        private static CardDefinition HeroPower() => TestFactory.ChooseYourWeaponsDefinition();

        private static Player One(GameEngine engine) => engine.State.GetPlayer(PlayerId.One);

        // ==================================================================
        //  The card itself
        // ==================================================================

        [Test]
        public void The_hero_power_is_a_one_mana_uncollectible_necromancer_card()
        {
            CardDefinition power = HeroPower();

            Assert.That(power.Id.Value, Is.EqualTo("necromancer_choose_your_weapons"));
            Assert.That(power.Type, Is.EqualTo(CardType.HeroPower));
            Assert.That(power.Class, Is.EqualTo(CardClass.Necromancer));
            Assert.That(power.ManaCost, Is.EqualTo(1));
            Assert.That(power.Collectible, Is.False);
        }

        [Test]
        public void It_offers_exactly_four_options_in_a_stable_order()
        {
            IReadOnlyList<EffectDefinition> options = HeroPowerOptions.Of(HeroPower());

            Assert.That(options.Count, Is.EqualTo(4));

            string[] expected =
            {
                "necromancer_skeletal_warrior",
                "necromancer_skeletal_rogue",
                "necromancer_crypt_fiend",
                "necromancer_abomination"
            };

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(options[index].Action.Kind, Is.EqualTo(EffectActionKind.Summon));
                Assert.That(options[index].Action.SummonCardId.Value, Is.EqualTo(expected[index]),
                    "Option " + index + " is not the servant it was authored as. The order is " +
                    "what a saved replay names its choice by, so it may not drift.");
            }
        }

        [Test]
        public void Reading_the_options_twice_gives_the_same_order()
        {
            IReadOnlyList<EffectDefinition> first = HeroPowerOptions.Of(HeroPower());
            IReadOnlyList<EffectDefinition> second = HeroPowerOptions.Of(HeroPower());

            Assert.That(first.Count, Is.EqualTo(second.Count));

            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(first[index].Action.SummonCardId, Is.EqualTo(second[index].Action.SummonCardId));
            }
        }

        // ==================================================================
        //  It reaches the match
        // ==================================================================

        [Test]
        public void A_configured_hero_power_is_given_to_that_seats_hero()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            Assert.That(One(engine).Hero.HeroPowerCardId.Value,
                Is.EqualTo(TestFactory.ChooseYourWeaponsCardId));

            Assert.That(engine.State.GetPlayer(PlayerId.Two).Hero.HasHeroPower, Is.False,
                "Seat two was configured with nothing and must have nothing.");
        }

        /// <summary>
        /// A match set up without hero powers behaves exactly as every match
        /// did before they existed. This is what makes the feature additive
        /// rather than a change to the rules.
        /// </summary>
        [Test]
        public void A_match_configured_without_hero_powers_has_none()
        {
            GameEngine engine = TestFactory.StartedMatch();

            Assert.That(One(engine).Hero.HasHeroPower, Is.False);
            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.NoHeroPower));
        }

        // ==================================================================
        //  Legality
        // ==================================================================

        [Test]
        public void The_owner_can_use_it_on_their_own_turn_with_mana_and_a_free_slot()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void The_opponent_cannot_use_it()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            Assert.That(engine.CanUseHeroPower(PlayerId.Two),
                Is.EqualTo(RejectionReason.NoHeroPower).Or.EqualTo(RejectionReason.NotYourTurn),
                "Seat two has no hero power of its own and is not the active player.");

            CommandResult result = engine.Execute(new UseHeroPowerCommand(PlayerId.Two, 0));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(One(engine).HasUsedHeroPowerThisTurn, Is.False,
                "A refused command must not consume the other player's hero power.");
        }

        [Test]
        public void It_cannot_be_used_outside_its_owners_turn()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            TestFactory.EndTurn(engine);

            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.NotYourTurn));

            CommandResult result = engine.Execute(new UseHeroPowerCommand(PlayerId.One, 0));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.NotYourTurn));
        }

        [Test]
        public void It_cannot_be_used_without_the_mana_for_it()
        {
            GameEngine engine = TestFactory.NecromancerMatch(mana: 0);

            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.NotEnoughMana));

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.NotEnoughMana));
            Assert.That(One(engine).Board.Count, Is.Zero);
        }

        [Test]
        public void It_cannot_be_used_with_a_full_board()
        {
            GameEngine engine = TestFactory.NecromancerMatch();
            Player player = One(engine);

            while (!player.Board.IsFull)
            {
                TestFactory.PutMinionOnBoard(engine, PlayerId.One);
            }

            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.BoardFull));

            int manaBefore = player.AvailableMana;
            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.BoardFull));

            // The point of checking before committing: a refused hero power
            // costs nothing at all.
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore));
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
        }

        [Test]
        public void It_can_only_be_used_once_a_turn()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            Assert.That(TestFactory.UseHeroPower(engine, 0).IsAccepted, Is.True);
            Assert.That(One(engine).HasUsedHeroPowerThisTurn, Is.True);

            Assert.That(engine.CanUseHeroPower(PlayerId.One),
                Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));

            int manaAfterFirst = One(engine).AvailableMana;
            int boardAfterFirst = One(engine).Board.Count;

            CommandResult second = TestFactory.UseHeroPower(engine, 1);

            Assert.That(second.IsAccepted, Is.False);
            Assert.That(second.Reason, Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));
            Assert.That(One(engine).AvailableMana, Is.EqualTo(manaAfterFirst));
            Assert.That(One(engine).Board.Count, Is.EqualTo(boardAfterFirst));
        }

        [Test]
        public void It_becomes_usable_again_on_the_owners_next_turn()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            TestFactory.UseHeroPower(engine, 0);
            Assert.That(engine.CanUseHeroPower(PlayerId.One),
                Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));

            TestFactory.AdvanceToNextTurnOf(engine, PlayerId.One);

            Assert.That(One(engine).HasUsedHeroPowerThisTurn, Is.False);
            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void An_option_outside_the_list_is_refused_and_costs_nothing()
        {
            GameEngine engine = TestFactory.NecromancerMatch();
            Player player = One(engine);

            int manaBefore = player.AvailableMana;

            foreach (int forged in new[] { -1, 4, 99 })
            {
                CommandResult result = TestFactory.UseHeroPower(engine, forged);

                Assert.That(result.IsAccepted, Is.False, "Option " + forged + " was accepted.");
                Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidHeroPowerOption));
            }

            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore));
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
            Assert.That(player.Board.Count, Is.Zero);
        }

        // ==================================================================
        //  Choosing
        // ==================================================================

        [Test]
        public void Each_option_summons_only_the_servant_it_names()
        {
            for (int index = 0; index < TestFactory.ServantCardIds.Length; index++)
            {
                GameEngine engine = TestFactory.NecromancerMatch();

                CommandResult result = TestFactory.UseHeroPower(engine, index);

                Assert.That(result.IsAccepted, Is.True);

                Player player = One(engine);

                Assert.That(player.Board.Count, Is.EqualTo(1),
                    "Option " + index + " put something other than exactly one minion down.");

                Assert.That(player.Board[0].CardId.Value,
                    Is.EqualTo(TestFactory.ServantCardIds[index]));
            }
        }

        [Test]
        public void The_chosen_servant_goes_straight_to_the_board_and_never_to_hand()
        {
            GameEngine engine = TestFactory.NecromancerMatch();
            Player player = One(engine);

            int handBefore = player.Hand.Count;

            TestFactory.UseHeroPower(engine, 0);

            Assert.That(player.Board.Count, Is.EqualTo(1));
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore),
                "The servant passed through the hand. A hero power summons; it does not draw.");
        }

        [Test]
        public void Only_the_hero_powers_mana_is_spent_never_the_servants_printed_cost()
        {
            GameEngine engine = TestFactory.NecromancerMatch(mana: 1);
            Player player = One(engine);

            Assert.That(TestFactory.SkeletalWarriorDefinition().ManaCost, Is.EqualTo(1),
                "This test is only meaningful while the servant has a printed cost of its own.");

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.True,
                "One mana was enough for the hero power, so it must have been enough.");

            Assert.That(player.AvailableMana, Is.Zero,
                "Exactly the hero power's cost was paid: one, not two.");

            Assert.That(player.Board.Count, Is.EqualTo(1));
        }

        [Test]
        public void Using_it_reports_the_power_and_then_the_summon()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            CommandResult result = TestFactory.UseHeroPower(engine, 3);

            int powerAt = IndexOf<HeroPowerUsedEvent>(result.Events);
            int summonAt = IndexOf<MinionSummonedEvent>(result.Events);

            Assert.That(powerAt, Is.GreaterThanOrEqualTo(0), "No HeroPowerUsed event was reported.");
            Assert.That(summonAt, Is.GreaterThan(powerAt),
                "The summon must be reported after the power that caused it.");

            HeroPowerUsedEvent used = (HeroPowerUsedEvent)result.Events[powerAt];

            Assert.That(used.PlayerId, Is.EqualTo(PlayerId.One));
            Assert.That(used.OptionIndex, Is.EqualTo(3));
            Assert.That(used.HeroPowerCardId.Value, Is.EqualTo(TestFactory.ChooseYourWeaponsCardId));
        }

        // ==================================================================
        //  Cancellation
        // ==================================================================

        /// <summary>
        /// Cancelling is not sending the command.
        ///
        /// The whole reason activation and choice are one command is that there
        /// is then no such thing as a half-used hero power: a player who opens
        /// the menu and closes it again has changed nothing, because nothing
        /// was ever submitted. This asserts that asking the engine whether the
        /// power *could* be used - which is what opening the menu does - leaves
        /// the match exactly as it was.
        /// </summary>
        [Test]
        public void Asking_whether_the_power_can_be_used_changes_nothing()
        {
            GameEngine engine = TestFactory.NecromancerMatch();
            Player player = One(engine);

            int manaBefore = player.AvailableMana;
            int boardBefore = player.Board.Count;

            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.None));
            Assert.That(engine.GetHeroPowerOptions(PlayerId.One).Count, Is.EqualTo(4));

            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore));
            Assert.That(player.Board.Count, Is.EqualTo(boardBefore));
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
        }

        // ==================================================================
        //  Determinism
        // ==================================================================

        [Test]
        public void The_same_seed_and_the_same_choice_produce_the_same_match()
        {
            string FingerprintAfterUsing(int option)
            {
                GameEngine engine = TestFactory.NecromancerMatch(seed: 4242UL);
                TestFactory.UseHeroPower(engine, option);
                return CoH.Core.Diagnostics.StateFingerprint.Of(engine.State);
            }

            Assert.That(FingerprintAfterUsing(2), Is.EqualTo(FingerprintAfterUsing(2)));
            Assert.That(FingerprintAfterUsing(2), Is.Not.EqualTo(FingerprintAfterUsing(0)),
                "Two different choices must not produce the same board.");
        }

        /// <summary>
        /// Picking an option consumes no randomness.
        ///
        /// Checked by using the power and then confirming the next random draw
        /// is the one an untouched match would have produced. If choosing ever
        /// started rolling dice, every shuffle after a hero power would differ
        /// and replays would drift for no visible reason.
        /// </summary>
        [Test]
        public void Choosing_an_option_consumes_no_randomness()
        {
            GameEngine used = TestFactory.NecromancerMatch(seed: 77UL);
            GameEngine untouched = TestFactory.NecromancerMatch(seed: 77UL);

            TestFactory.UseHeroPower(used, 1);

            for (int draw = 0; draw < 8; draw++)
            {
                Assert.That(used.State.RandomSource.NextInt(1000),
                    Is.EqualTo(untouched.State.RandomSource.NextInt(1000)),
                    "The random stream moved. Draw " + draw + " differs after a hero power.");
            }
        }

        private static int IndexOf<T>(IReadOnlyList<GameEvent> events) where T : GameEvent
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] is T)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
