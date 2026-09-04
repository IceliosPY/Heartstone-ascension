using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Spell Damage as a generic rules concept, not "what Lunar Phase does".
    ///
    /// Lunar Phase is the only thing granting it today, but every test here
    /// reaches the modifier through <see cref="Player.SpellDamageBonus"/> and
    /// ordinary damaging spells - the same test cards <c>TestFactory</c>
    /// already builds for other suites - rather than through the hero power
    /// itself, so a second source added later would be provable the same
    /// way.
    /// </summary>
    public sealed class SpellDamageTests
    {
        private static Player One(GameEngine engine) => engine.State.GetPlayer(PlayerId.One);

        private static Player Two(GameEngine engine) => engine.State.GetPlayer(PlayerId.Two);

        /// <summary>Plays a targeted spell for the active player, aimed at the given minion.</summary>
        private static CommandResult PlayTargetedSpellAt(GameEngine engine, PlayerId caster, EntityId targetId)
        {
            CardInstance spell = TestFactory.PutCardInHand(engine, caster, "test_targeted_spell");
            return engine.Execute(new PlayCardCommand(caster, spell.Id, PlayCardCommand.Rightmost, targetId));
        }

        // ==================================================================
        //  The core rule: base + controller's bonus, at the spell boundary
        // ==================================================================

        [Test]
        public void Spell_damage_increases_a_damaging_spells_amount()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            engine.State.GetPlayer(caster).SpellDamageBonus = 1;

            Assert.That(TestFactory.SpellDefinition().ManaCost, Is.GreaterThan(0));

            CommandResult result = PlayTargetedSpellAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(TestFactory.TargetedSpellDefinition().Effects[0].Action.Amount + 1),
                "Base spell damage (3) plus the controller's Spell Damage (1) must equal 4.");
        }

        [Test]
        public void With_no_spell_damage_a_spell_deals_exactly_its_printed_amount()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            PlayTargetedSpellAt(engine, caster, target.Id);

            Assert.That(target.Damage, Is.EqualTo(3));
        }

        [Test]
        public void Spell_damage_scales_a_multi_target_sweep_on_every_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion first = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);
            Minion second = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            engine.State.GetPlayer(caster).SpellDamageBonus = 1;

            // test_aoe: 1 damage to every enemy minion.
            CardInstance sweep = TestFactory.PutCardInHand(engine, caster, "test_aoe");
            CommandResult result = engine.Execute(new PlayCardCommand(caster, sweep.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(first.Damage, Is.EqualTo(2), "Base 1 plus the +1 bonus, applied to every target the sweep hits.");
            Assert.That(second.Damage, Is.EqualTo(2));
        }

        // ==================================================================
        //  Ownership: the caster's bonus, never the opponent's
        // ==================================================================

        [Test]
        public void Spell_damage_does_not_affect_the_opponents_spells()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            PlayerId opponent = caster.Opponent;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, opponent, health: 20);

            // The bonus belongs to the OPPONENT, not to whoever is about to cast.
            engine.State.GetPlayer(opponent).SpellDamageBonus = 5;

            PlayTargetedSpellAt(engine, caster, target.Id);

            Assert.That(target.Damage, Is.EqualTo(3),
                "The caster's spell must not read a bonus that belongs to the other player.");
        }

        // ==================================================================
        //  Lifecycle: this-turn only
        // ==================================================================

        [Test]
        public void Spell_damage_expires_at_the_end_of_the_grantees_turn()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            TestFactory.UseHeroPower(engine, 0);
            Assert.That(Two(engine).SpellDamageBonus, Is.EqualTo(1));

            TestFactory.EndTurn(engine);

            Assert.That(Two(engine).SpellDamageBonus, Is.Zero,
                "Spell Damage must be gone the instant the granting player's turn ends, before the " +
                "opponent's turn is even under way.");
        }

        [Test]
        public void Spell_damage_is_not_active_during_the_opponents_following_turn()
        {
            GameEngine engine = TestFactory.StarcallerMatch();
            PlayerId starcaller = PlayerId.Two;
            PlayerId opponent = PlayerId.One;

            TestFactory.UseHeroPower(engine, 0);
            TestFactory.EndTurn(engine);

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(opponent));
            Assert.That(engine.State.GetPlayer(opponent).SpellDamageBonus, Is.Zero);
            Assert.That(engine.State.GetPlayer(starcaller).SpellDamageBonus, Is.Zero);

            TestFactory.GiveMana(engine, opponent, 10);
            Minion target = TestFactory.PutMinionOnBoard(engine, starcaller, health: 20);

            PlayTargetedSpellAt(engine, opponent, target.Id);

            Assert.That(target.Damage, Is.EqualTo(3),
                "The opponent's own spell, cast during their turn right after Lunar Phase, must not " +
                "still be boosted.");
        }

        [Test]
        public void Spell_damage_is_absent_on_the_grantees_next_turn_unless_used_again()
        {
            GameEngine engine = TestFactory.StarcallerMatch();

            TestFactory.UseHeroPower(engine, 0);
            TestFactory.AdvanceToNextTurnOf(engine, PlayerId.Two);

            Assert.That(Two(engine).SpellDamageBonus, Is.Zero,
                "Without using Lunar Phase again, its previous grant must not still be active.");

            TestFactory.GiveMana(engine, PlayerId.Two, 10);
            Minion target = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 20);

            PlayTargetedSpellAt(engine, PlayerId.Two, target.Id);

            Assert.That(target.Damage, Is.EqualTo(3));
        }

        // ==================================================================
        //  Boundaries: what Spell Damage must never touch
        // ==================================================================

        [Test]
        public void Spell_damage_does_not_increase_a_non_damage_spell_effect()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = 3;

            int manaBefore = player.AvailableMana;

            // The Coin: OnPlay, gain 1 temporary mana - not damage.
            CardInstance coin = TestFactory.PutCardInHand(engine, caster, TestFactory.CoinCardId.Value);
            CommandResult result = engine.Execute(new PlayCardCommand(caster, coin.Id));

            Assert.That(result.IsAccepted, Is.True);

            // The Coin costs 0 and grants 1 temporary mana, so available mana
            // should be exactly 1 higher than before - never boosted by the
            // unrelated Spell Damage bonus sitting on the same player.
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore + 1),
                "Spell Damage leaked into a non-damage numeric effect.");
        }

        [Test]
        public void Spell_damage_does_not_increase_hero_power_damage()
        {
            GameConfig config = GameConfig.Default.WithHeroPowers(
                new CardId("test_hero_power_damage"), default);

            GameEngine engine = TestFactory.StartedMatch(config: config);

            if (engine.State.CurrentPlayer != PlayerId.One)
            {
                TestFactory.EndTurn(engine);
            }

            Player player = One(engine);
            TestFactory.GiveMana(engine, PlayerId.One, 10);
            player.SpellDamageBonus = 5;

            Hero enemyHero = TestFactory.EnemyHero(engine);
            int healthBefore = enemyHero.CurrentHealth;

            CommandResult result = TestFactory.UseHeroPower(engine, 0);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(healthBefore - enemyHero.CurrentHealth, Is.EqualTo(2),
                "A hero power's own damage must never receive Spell Damage merely because both deal " +
                "damage - only a damaging spell (EffectTrigger.OnPlay) may.");
        }
    }
}
