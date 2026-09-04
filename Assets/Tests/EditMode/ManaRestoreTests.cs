using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Restore gives back spent mana without creating crystals and without
    /// taking away a temporary surplus that is already in the live pool.
    /// </summary>
    public sealed class ManaRestoreTests
    {
        private static GameEngine Ready(
            out PlayerId active,
            out Player player,
            int maxMana,
            int availableMana,
            int temporaryMana = 0)
        {
            GameEngine engine = TestFactory.StartedMatch();
            active = engine.State.CurrentPlayer;
            player = engine.State.GetPlayer(active);
            player.MaxMana = maxMana;
            player.AvailableMana = availableMana;
            player.TemporaryMana = temporaryMana;
            return engine;
        }

        private static void Restore(GameEngine engine, Player player, int amount) =>
            ManaSystem.Restore(new ResolutionContext(engine.State), player, amount);

        [Test]
        public void Spending_two_then_restoring_one_leaves_four_of_five()
        {
            GameEngine engine = Ready(out _, out Player player, maxMana: 5, availableMana: 5);
            ResolutionContext context = new ResolutionContext(engine.State);

            ManaSystem.Pay(context, player, 2);
            ManaSystem.Restore(context, player, 1);

            Assert.That(player.AvailableMana, Is.EqualTo(4));
            Assert.That(player.MaxMana, Is.EqualTo(5));
        }

        [Test]
        public void Restoring_above_the_permanent_cap_never_reduces_temporary_mana()
        {
            GameEngine engine = Ready(
                out _, out Player player, maxMana: 5, availableMana: 6, temporaryMana: 1);

            Restore(engine, player, 1);

            Assert.That(player.AvailableMana, Is.EqualTo(6));
            Assert.That(player.TemporaryMana, Is.EqualTo(1));
        }

        [Test]
        public void Restoring_while_below_the_permanent_cap_adds_the_requested_amount()
        {
            GameEngine engine = Ready(out _, out Player player, maxMana: 5, availableMana: 1);

            Restore(engine, player, 2);

            Assert.That(player.AvailableMana, Is.EqualTo(3));
        }

        [Test]
        public void Restoring_never_creates_permanent_max_mana()
        {
            GameEngine engine = Ready(out _, out Player player, maxMana: 5, availableMana: 1);

            Restore(engine, player, 20);

            Assert.That(player.AvailableMana, Is.EqualTo(5));
            Assert.That(player.MaxMana, Is.EqualTo(5));
            Assert.That(player.TemporaryMana, Is.Zero);
        }

        [Test]
        public void Huntress_shot_still_restores_mana_through_the_generic_rule()
        {
            GameEngine engine = Ready(out PlayerId active, out Player player, maxMana: 5, availableMana: 5);
            player.SpellDamageBonus = 1;

            Minion target = TestFactory.PutMinionOnBoard(engine, active.Opponent, health: 20);
            CardInstance shot = TestFactory.PutCardInHand(engine, active, TestFactory.HuntressShotCardId);

            CommandResult result = engine.Execute(
                new PlayCardCommand(active, shot.Id, PlayCardCommand.Rightmost, target.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(player.AvailableMana, Is.EqualTo(3), "5 - 3 + 1 restored.");
            Assert.That(player.MaxMana, Is.EqualTo(5));
        }

        [Test]
        public void Coin_then_restore_is_non_destructive()
        {
            GameEngine engine = Ready(out PlayerId active, out Player player, maxMana: 5, availableMana: 5);
            CardInstance coin = TestFactory.PutCardInHand(engine, active, TestFactory.CoinCardId.Value);

            Assert.That(TestFactory.PlayCard(engine, coin.Id).IsAccepted, Is.True);
            Assert.That(player.AvailableMana, Is.EqualTo(6));

            Restore(engine, player, 1);

            Assert.That(player.AvailableMana, Is.EqualTo(6));
            Assert.That(player.MaxMana, Is.EqualTo(5));
            Assert.That(player.TemporaryMana, Is.EqualTo(1));
        }

        [Test]
        public void End_of_turn_still_removes_temporary_mana()
        {
            GameEngine engine = Ready(out PlayerId active, out Player player, maxMana: 5, availableMana: 5);
            CardInstance coin = TestFactory.PutCardInHand(engine, active, TestFactory.CoinCardId.Value);

            TestFactory.PlayCard(engine, coin.Id);
            Restore(engine, player, 1);
            TestFactory.AdvanceToNextTurnOf(engine, active);

            Assert.That(player.TemporaryMana, Is.Zero);
            Assert.That(player.AvailableMana, Is.EqualTo(player.MaxMana));
        }
    }
}
