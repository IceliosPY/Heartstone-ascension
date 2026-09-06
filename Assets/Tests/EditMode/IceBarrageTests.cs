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
    /// Necromancer's second collectible spell, Ice Barrage: 1 damage to a
    /// chosen enemy minion, then Freeze it. The mechanism itself (Case A/B/C,
    /// refreezing, thawing) is proven generically in <see cref="FreezeTests"/>;
    /// these prove only that the card wires into it correctly, with no
    /// special-casing by card id anywhere.
    /// </summary>
    public sealed class IceBarrageTests
    {
        private static CardDefinition Definition() => TestFactory.IceBarrageDefinition();

        private static CardInstance IceBarrageInHand(GameEngine engine, PlayerId caster) =>
            TestFactory.PutCardInHand(engine, caster, TestFactory.IceBarrageCardId);

        private static CommandResult PlayIceBarrageAt(GameEngine engine, PlayerId caster, EntityId targetId)
        {
            CardInstance card = IceBarrageInHand(engine, caster);
            return engine.Execute(new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, targetId));
        }

        // ==================================================================
        //  1-5. Card identity
        // ==================================================================

        [Test]
        public void Card_id_is_correct() =>
            Assert.That(Definition().Id.Value, Is.EqualTo("necromancer_ice_barrage"));

        [Test]
        public void Name_is_ice_barrage() =>
            Assert.That(Definition().Name, Is.EqualTo("Ice Barrage"));

        [Test]
        public void Belongs_to_Necromancer() =>
            Assert.That(Definition().Class, Is.EqualTo(CardClass.Necromancer));

        [Test]
        public void Is_a_spell() =>
            Assert.That(Definition().Type, Is.EqualTo(CardType.Spell));

        [Test]
        public void Costs_two_mana() =>
            Assert.That(Definition().ManaCost, Is.EqualTo(2));

        [Test]
        public void Is_collectible() =>
            Assert.That(Definition().Collectible, Is.True);

        // ==================================================================
        //  6-10. Targeting - enemy minion only, exactly
        // ==================================================================

        [Test]
        public void An_enemy_minion_is_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion enemy = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CommandResult result = PlayIceBarrageAt(engine, caster, enemy.Id);

            Assert.That(result.IsAccepted, Is.True);
        }

        [Test]
        public void An_enemy_hero_is_not_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = IceBarrageInHand(engine, caster);
            EntityId enemyHero = engine.State.GetPlayer(caster.Opponent).Hero.Id;

            RejectionReason reason = engine.CanExecute(
                new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, enemyHero));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void A_friendly_minion_is_not_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            Minion friendly = TestFactory.PutMinionOnBoard(engine, caster, health: 10);
            CardInstance card = IceBarrageInHand(engine, caster);

            RejectionReason reason = engine.CanExecute(
                new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, friendly.Id));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void A_friendly_hero_is_not_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = IceBarrageInHand(engine, caster);
            EntityId friendlyHero = engine.State.GetPlayer(caster).Hero.Id;

            RejectionReason reason = engine.CanExecute(
                new PlayCardCommand(caster, card.Id, PlayCardCommand.Rightmost, friendlyHero));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void No_target_is_illegal_when_an_enemy_minion_is_available()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);
            TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CardInstance card = IceBarrageInHand(engine, caster);

            RejectionReason reason = engine.CanExecute(new PlayCardCommand(caster, card.Id));

            Assert.That(reason, Is.EqualTo(RejectionReason.InvalidTarget));
        }

        // ==================================================================
        //  11-13. Resolution: damage, Spell Damage scaling, Freeze
        // ==================================================================

        [Test]
        public void Deals_one_damage_without_spell_damage()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10);

            CommandResult result = PlayIceBarrageAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(1));
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        public void Damage_scales_with_spell_damage_with_no_special_case(int spellDamage, int expectedDamage)
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(caster);

            TestFactory.GiveMana(engine, caster, 10);
            player.SpellDamageBonus = spellDamage;

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 20);

            CommandResult result = PlayIceBarrageAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.Damage, Is.EqualTo(expectedDamage));
        }

        [Test]
        public void Freezes_the_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10, ready: true);

            CommandResult result = PlayIceBarrageAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.IsFrozen, Is.True);
        }

        [Test]
        public void Nothing_else_on_the_board_is_affected()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10, ready: true);
            Minion otherEnemy = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 10, ready: true);
            Minion friendly = TestFactory.PutMinionOnBoard(engine, caster, health: 10, ready: true);

            PlayIceBarrageAt(engine, caster, target.Id);

            Assert.That(otherEnemy.Damage, Is.Zero);
            Assert.That(otherEnemy.IsFrozen, Is.False);
            Assert.That(friendly.Damage, Is.Zero);
            Assert.That(friendly.IsFrozen, Is.False);
        }

        // ==================================================================
        //  14. A target killed by the damage is a safe no-op for Freeze
        // ==================================================================

        [Test]
        public void A_target_killed_by_the_damage_is_removed_and_the_freeze_that_follows_is_a_no_op()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId caster = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, caster, 10);

            // One health: the spell's own 1 damage kills it outright, before
            // the Freeze row resolves - both rows share the same resolved
            // target list (see SelectorResolver), so Freeze still names this
            // now-dead minion rather than silently picking another.
            Minion target = TestFactory.PutMinionOnBoard(engine, caster.Opponent, health: 1, ready: true);

            CommandResult result = PlayIceBarrageAt(engine, caster, target.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(target.IsInPlay, Is.False, "The lethal damage must have removed it from the board.");
            Assert.That(target.IsFrozen, Is.False,
                "FreezeRules guards a target no longer in play exactly as DamageRules already does, " +
                "so the Freeze that follows lethal damage is a safe no-op rather than a special case.");
        }
    }
}
