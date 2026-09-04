using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Starcaller's hero power: what it costs, when it may be used, and what
    /// using it actually grants.
    ///
    /// Deliberately not "the second class". A hero power with exactly one
    /// option, resolving an effect that is not a summon, is the mechanism
    /// this whole class was chosen to exercise - the same generic
    /// infrastructure Raise uses, with no branch anywhere that knows Lunar
    /// Phase's card id or that its one option grants Spell Damage rather
    /// than summoning.
    /// </summary>
    public sealed class StarcallerHeroPowerTests
    {
        private static CardDefinition HeroPower() => TestFactory.LunarPhaseDefinition();

        private static Player Two(GameEngine engine) => engine.State.GetPlayer(PlayerId.Two);

        // ==================================================================
        //  The card itself
        // ==================================================================

        [Test]
        public void The_hero_power_is_a_two_mana_uncollectible_starcaller_card()
        {
            CardDefinition power = HeroPower();

            Assert.That(power.Id.Value, Is.EqualTo("starcaller_lunar_phase"));
            Assert.That(power.Type, Is.EqualTo(CardType.HeroPower));
            Assert.That(power.Class, Is.EqualTo(CardClass.Starcaller));
            Assert.That(power.ManaCost, Is.EqualTo(2));
            Assert.That(power.Collectible, Is.False);
        }

        [Test]
        public void It_offers_exactly_one_option_that_grants_spell_damage()
        {
            IReadOnlyList<EffectDefinition> options = HeroPowerOptions.Of(HeroPower());

            Assert.That(options.Count, Is.EqualTo(1),
                "Lunar Phase has nothing to choose between - a single row is the whole menu.");

            Assert.That(options[0].Action.Kind, Is.EqualTo(EffectActionKind.GrantSpellDamage));
            Assert.That(options[0].Action.Amount, Is.EqualTo(1));
        }

        // ==================================================================
        //  It reaches the match
        // ==================================================================

        [Test]
        public void A_configured_hero_power_is_given_to_that_seats_hero()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            Assert.That(Two(engine).Hero.HeroPowerCardId.Value, Is.EqualTo(TestFactory.LunarPhaseCardId));
        }

        // ==================================================================
        //  Legality - the same rules Raise already proved, on a different seat
        // ==================================================================

        [Test]
        public void The_owner_can_use_it_on_their_own_turn_with_mana()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void The_opponent_cannot_use_it()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            CommandResult result = engine.Execute(new UseHeroPowerCommand(PlayerId.One, 0));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(Two(engine).HasUsedHeroPowerThisTurn, Is.False);
        }

        [Test]
        public void It_cannot_be_used_with_less_than_two_mana()
        {
            GameEngine engine = TestFactory.StarcallerMatch(mana: 1);

            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.NotEnoughMana));

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.NotEnoughMana));
            Assert.That(Two(engine).SpellDamageBonus, Is.Zero);
        }

        [Test]
        public void It_spends_exactly_two_mana()
        {
            GameEngine engine = TestFactory.StarcallerMatch(mana: 5);

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(Two(engine).AvailableMana, Is.EqualTo(3));
        }

        [Test]
        public void It_can_only_be_used_once_a_turn()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            Assert.That(TestFactory.UseHeroPower(engine, 0).IsAccepted, Is.True);
            Assert.That(Two(engine).HasUsedHeroPowerThisTurn, Is.True);

            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));

            int manaAfterFirst = Two(engine).AvailableMana;
            int bonusAfterFirst = Two(engine).SpellDamageBonus;

            CommandResult second = TestFactory.UseHeroPower(engine, 0);

            Assert.That(second.IsAccepted, Is.False);
            Assert.That(second.Reason, Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));
            Assert.That(Two(engine).AvailableMana, Is.EqualTo(manaAfterFirst));
            Assert.That(Two(engine).SpellDamageBonus, Is.EqualTo(bonusAfterFirst),
                "A refused second use must not grant a second stack of Spell Damage.");
        }

        [Test]
        public void It_becomes_usable_again_on_the_owners_next_turn()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            TestFactory.UseHeroPower(engine, 0);
            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));

            TestFactory.AdvanceToNextTurnOf(engine, PlayerId.Two);

            Assert.That(Two(engine).HasUsedHeroPowerThisTurn, Is.False);
            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.None));
        }

        // ==================================================================
        //  What it grants
        // ==================================================================

        [Test]
        public void Using_it_grants_exactly_one_spell_damage()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            Assert.That(Two(engine).SpellDamageBonus, Is.Zero);

            TestFactory.UseHeroPower(engine, 0);

            Assert.That(Two(engine).SpellDamageBonus, Is.EqualTo(1));
        }

        [Test]
        public void It_does_not_touch_the_board_or_the_hand()
        {
            GameEngine engine = TestFactory.StarcallerMatch();
            Player player = Two(engine);

            int boardBefore = player.Board.Count;
            int handBefore = player.Hand.Count;

            TestFactory.UseHeroPower(engine, 0);

            Assert.That(player.Board.Count, Is.EqualTo(boardBefore));
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore));
        }

        [Test]
        public void Using_it_reports_the_power_and_the_grant()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            int powerAt = IndexOf<HeroPowerUsedEvent>(result.Events);
            int grantAt = IndexOf<SpellDamageGrantedEvent>(result.Events);

            Assert.That(powerAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(grantAt, Is.GreaterThan(powerAt),
                "The grant must be reported after the power that caused it.");

            HeroPowerUsedEvent used = (HeroPowerUsedEvent)result.Events[powerAt];
            Assert.That(used.PlayerId, Is.EqualTo(PlayerId.Two));
            Assert.That(used.HeroPowerCardId.Value, Is.EqualTo(TestFactory.LunarPhaseCardId));

            SpellDamageGrantedEvent granted = (SpellDamageGrantedEvent)result.Events[grantAt];
            Assert.That(granted.PlayerId, Is.EqualTo(PlayerId.Two));
            Assert.That(granted.Amount, Is.EqualTo(1));
            Assert.That(granted.NewTotal, Is.EqualTo(1));
        }

        // ==================================================================
        //  Determinism
        // ==================================================================

        [Test]
        public void The_same_seed_produces_the_same_match()
        {
            string FingerprintAfterUsing()
            {
                GameEngine engine = TestFactory.StarcallerMatch(seed: 4242UL);
                TestFactory.UseHeroPower(engine, 0);
                return CoH.Core.Diagnostics.StateFingerprint.Of(engine.State);
            }

            Assert.That(FingerprintAfterUsing(), Is.EqualTo(FingerprintAfterUsing()));
        }

        [Test]
        public void Using_it_consumes_no_randomness()
        {
            GameEngine used = TestFactory.StarcallerMatch(seed: 77UL);
            GameEngine untouched = TestFactory.StarcallerMatch(seed: 77UL);

            TestFactory.UseHeroPower(used, 0);

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
