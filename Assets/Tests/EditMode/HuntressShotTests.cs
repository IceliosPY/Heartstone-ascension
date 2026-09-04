using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Starcaller's first collectible spell, Huntress Shot: 1 damage to a
    /// chosen minion, then mana restored equal to the caster's current
    /// Spell Damage - two independent numbers, proven independent here
    /// rather than assumed.
    /// </summary>
    public sealed class HuntressShotTests
    {
        private static CardDefinition Definition() => TestFactory.HuntressShotDefinition();

        /// <summary>Puts Huntress Shot in the active player's hand.</summary>
        private static CardInstance HuntressShotInHand(GameEngine engine, PlayerId caster) =>
            TestFactory.PutCardInHand(engine, caster, TestFactory.HuntressShotCardId);

        /// <summary>Plays Huntress Shot for the active player, aimed at the given target.</summary>
        private static CommandResult PlayHuntressShotAt(GameEngine engine, PlayerId caster, EntityId targetId)
        {
            CardInstance card = HuntressShotInHand(engine, caster);
            return engine.Execute(new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, targetId));
        }

        // ==================================================================
        //  1-5. Card identity
        // ==================================================================

        [Test]
        public void Card_id_is_correct() =>
            Assert.That(Definition().Id.Value, Is.EqualTo("starcaller_huntress_shot"));

        [Test]
        public void Belongs_to_Starcaller() =>
            Assert.That(Definition().Class, Is.EqualTo(CardClass.Starcaller));

        [Test]
        public void Is_a_spell() =>
            Assert.That(Definition().Type, Is.EqualTo(CardType.Spell));

        [Test]
        public void Costs_three_mana() =>
            Assert.That(Definition().ManaCost, Is.EqualTo(3));

        [Test]
        public void Is_collectible() =>
            Assert.That(Definition().Collectible, Is.True);

        // ==================================================================
        //  6-11. Targeting
        // ==================================================================

        [Test]
        public void Requires_a_minion_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = HuntressShotInHand(engine, caster);

            Assert.That(engine.GetPlayTargetRequirement(caster, card.Id), Is.EqualTo(PlayTargetRequirement.Required));
        }

        [Test]
        public void A_friendly_minion_is_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion friendly = TestFactory.PutMinionOnBoard(engine, caster, health: 10);

            CommandResult result = PlayHuntressShotAt(engine, caster, friendly.Id);

            Assert.That(result.IsAccepted, Is.True);
        }

        [Test]
        public void An_enemy_minion_is_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion enemy = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CommandResult result = PlayHuntressShotAt(engine, caster, enemy.Id);

            Assert.That(result.IsAccepted, Is.True);
        }

        [Test]
        public void A_friendly_hero_is_not_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = HuntressShotInHand(engine, caster);
            EntityId friendlyHero = engine.State.GetPlayer(caster).Hero.Id;

            RejectionReason reason = engine.CanExecute(
                new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, friendlyHero));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void An_enemy_hero_is_not_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = HuntressShotInHand(engine, caster);
            EntityId enemyHero = engine.State.GetPlayer(caster.Opponent).Hero.Id;

            RejectionReason reason = engine.CanExecute(
                new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, enemyHero));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void No_target_is_illegal_when_a_minion_is_available()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = HuntressShotInHand(engine, caster);

            RejectionReason reason = engine.CanExecute(new PlayCardCommand(caster, card.Id));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        // ==================================================================
        //  12. Cost
        // ==================================================================

        [Test]
        public void Playing_it_spends_exactly_three_mana()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);
            int manaBefore = engine.State.GetPlayer(caster).AvailableMana;

            PlayHuntressShotAt(engine, caster, target.Id);

            // The restore happens after the cost is paid, so what is left
            // over already reflects both: 10 - 3 spent + 0 restored (no
            // Spell Damage here) = 7.
            Assert.That(manaBefore - engine.State.GetPlayer(caster).AvailableMana, Is.EqualTo(3));
        }

        // ==================================================================
        //  13-15. Damage and restoration scale with Spell Damage - two
        //  independent numbers, read from the same bonus.
        // ==================================================================

        [TestCase(0, 1, 0)]
        [TestCase(1, 2, 1)]
        [TestCase(2, 3, 2)]
        public void Damage_and_restoration_scale_with_spell_damage(
            int spellDamage, int expectedDamage, int expectedRestored)
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = spellDamage;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);
            int manaAfterCost = player.AvailableMana - 3;

            CommandResult result = PlayHuntressShotAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(expectedDamage));
            Assert.That(player.AvailableMana, Is.EqualTo(manaAfterCost + expectedRestored));
        }

        // ==================================================================
        //  16-17. Restoration is a refund, never a crystal
        // ==================================================================

        [Test]
        public void Restored_mana_never_exceeds_max_mana()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            player.MaxMana = 5;
            player.AvailableMana = 3;
            player.SpellDamageBonus = 10;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            CommandResult result = PlayHuntressShotAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            // 3 available, pay 3 for the spell -> 0, then restore up to 10,
            // but the cap is MaxMana (5), never more.
            Assert.That(player.AvailableMana, Is.EqualTo(5));
        }

        [Test]
        public void Restoring_mana_does_not_raise_max_mana()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            player.MaxMana = 5;
            player.AvailableMana = 5;
            player.SpellDamageBonus = 3;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            PlayHuntressShotAt(engine, caster, target.Id);

            Assert.That(player.MaxMana, Is.EqualTo(5), "Restoring mana must never grow the crystal count itself.");
        }

        // ==================================================================
        //  18-19. Damage and mana restoration are independent calculations
        // ==================================================================

        [Test]
        public void Damage_and_restoration_are_independent_calculations()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = 2;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);
            int manaAfterCost = player.AvailableMana - 3;

            PlayHuntressShotAt(engine, caster, target.Id);

            // FinalDamage = base(1) + SpellDamage(2) = 3. ManaRestore =
            // SpellDamage(2) directly - not derived from the 3 damage dealt.
            Assert.That(target.Damage, Is.EqualTo(3));
            Assert.That(player.AvailableMana, Is.EqualTo(manaAfterCost + 2));
        }

        [Test]
        public void Overkilling_the_target_does_not_change_how_much_mana_is_restored()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = 2;

            // Only 1 health: the 3 damage massively overkills it.
            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 1);
            int manaAfterCost = player.AvailableMana - 3;

            PlayHuntressShotAt(engine, caster, target.Id);

            Assert.That(player.AvailableMana, Is.EqualTo(manaAfterCost + 2),
                "Mana restored must come from the caster's Spell Damage, not from the target's " +
                "remaining health, overkill, or the damage the target actually absorbed before dying.");
        }

        // ==================================================================
        //  20. Lunar Phase synergy, worked through end to end
        // ==================================================================

        [Test]
        public void Lunar_phase_then_huntress_shot_matches_the_worked_example()
        {
            GameEngine engine = TestFactory.StarcallerMatch(mana: 5);
            Player player = engine.State.GetPlayer(PlayerId.Two);

            Minion target = TestFactory.PutMinionOnBoard(engine, PlayerId.One, health: 20);

            Assert.That(player.AvailableMana, Is.EqualTo(5));

            // Lunar Phase: 2 mana, Spell Damage +1.
            CommandResult heroPower = TestFactory.UseHeroPower(engine, 0);
            Assert.That(heroPower.IsAccepted, Is.True);
            Assert.That(player.AvailableMana, Is.EqualTo(3));
            Assert.That(player.SpellDamageBonus, Is.EqualTo(1));

            // Huntress Shot: 3 mana. 1 base damage + 1 Spell Damage = 2. Restore 1.
            CommandResult shot = PlayHuntressShotAt(engine, PlayerId.Two, target.Id);

            Assert.That(shot.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(2));
            Assert.That(player.AvailableMana, Is.EqualTo(1), "3 - 3 (Huntress Shot) + 1 (restored) = 1.");

            // Lunar Phase's own Spell Damage is not consumed by casting a spell.
            Assert.That(player.SpellDamageBonus, Is.EqualTo(1),
                "Huntress Shot must not consume the Spell Damage it just benefited from.");

            TestFactory.EndTurn(engine);

            Assert.That(player.SpellDamageBonus, Is.Zero,
                "Spell Damage still expires normally at the end of the granting player's turn.");
        }

        // ==================================================================
        //  21. The generic mechanism does not disturb existing Spell Damage
        // ==================================================================

        [Test]
        public void Existing_fixed_amount_spells_are_unaffected_by_the_new_value_source()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = 1;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            CardInstance spell = TestFactory.PutCardInHand(engine, caster, "test_targeted_spell");
            CommandResult result = engine.Execute(
                new PlayCardCommand(caster, spell.Id, PlayCardCommand.Rightmost, target.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(TestFactory.TargetedSpellDefinition().Effects[0].Action.Amount + 1),
                "A plain Fixed-amount spell (test_targeted_spell) must still just add the bonus to its " +
                "own printed number, unaffected by the new AmountSource mechanism existing at all.");
        }

        [Test]
        public void The_restore_mana_action_describes_itself_with_its_value_source()
        {
            EffectActionDefinition restore = new EffectActionDefinition(
                EffectActionKind.RestoreMana, amountSource: EffectValueSource.SpellDamage);

            Assert.That(restore.Describe(), Is.EqualTo("RestoreMana(SpellDamage)"));

            EffectActionDefinition fixedRestore = new EffectActionDefinition(EffectActionKind.RestoreMana, amount: 2);

            Assert.That(fixedRestore.Describe(), Is.EqualTo("RestoreMana(2)"));
        }
    }
}
