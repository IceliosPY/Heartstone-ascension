using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The real development match configuration, reproduced from
    /// <c>MatchBootstrap</c>'s own defaults
    /// (<c>DefaultDevelopmentHeroPower</c> / <c>DefaultDevelopmentHeroPowerSeatTwo</c>):
    /// player one Necromancer/Raise, player two Starcaller/Lunar Phase.
    ///
    /// Every assertion here resolves a player's class the only way the
    /// engine ever does: <c>Player.Hero.HeroPowerCardId</c> looked up in the
    /// catalog for its <see cref="CardDefinition.Class"/>. There is no
    /// <c>Player.Class</c> field and there must never be an
    /// <c>if (playerId == PlayerId.Two)</c> anywhere in the rules - a
    /// player's class is what their hero power's own card says it is, full
    /// stop.
    /// </summary>
    public sealed class DevelopmentMatchConfigurationTests
    {
        private static CardClass ClassOf(GameEngine engine, PlayerId seat)
        {
            Player player = engine.State.GetPlayer(seat);

            Assert.That(player.Hero.HasHeroPower, Is.True, seat + " has no hero power configured at all.");
            Assert.That(engine.State.Catalog.TryGet(player.Hero.HeroPowerCardId, out CardDefinition definition),
                Is.True, seat + "'s hero power card id is not in the catalog.");

            return definition.Class;
        }

        [Test]
        public void Player_one_resolves_as_necromancer()
        {
            GameEngine engine = TestFactory.DevelopmentMatch();

            Assert.That(ClassOf(engine, PlayerId.One), Is.EqualTo(CardClass.Necromancer));
        }

        [Test]
        public void Player_two_resolves_as_starcaller()
        {
            GameEngine engine = TestFactory.DevelopmentMatch();

            Assert.That(ClassOf(engine, PlayerId.Two), Is.EqualTo(CardClass.Starcaller));
        }

        [Test]
        public void Player_one_resolves_raise()
        {
            GameEngine engine = TestFactory.DevelopmentMatch();

            Assert.That(
                engine.State.GetPlayer(PlayerId.One).Hero.HeroPowerCardId.Value,
                Is.EqualTo(TestFactory.ChooseYourWeaponsCardId));
        }

        [Test]
        public void Player_two_resolves_lunar_phase()
        {
            GameEngine engine = TestFactory.DevelopmentMatch();

            Assert.That(
                engine.State.GetPlayer(PlayerId.Two).Hero.HeroPowerCardId.Value,
                Is.EqualTo(TestFactory.LunarPhaseCardId));
        }

        /// <summary>
        /// Both hero powers work, at once, in the same match - not "either
        /// class in isolation", which is the scenario every other test in
        /// this pass otherwise uses.
        /// </summary>
        [Test]
        public void Player_one_can_use_raise_and_player_two_can_later_use_lunar_phase()
        {
            GameEngine engine = TestFactory.DevelopmentMatch();

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(PlayerId.One));
            Assert.That(engine.CanUseHeroPower(PlayerId.One), Is.EqualTo(RejectionReason.None));

            CommandResult raise = TestFactory.UseHeroPower(engine, 0);
            Assert.That(raise.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(1));

            TestFactory.EndTurn(engine);

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));
            Assert.That(engine.CanUseHeroPower(PlayerId.Two), Is.EqualTo(RejectionReason.None));

            CommandResult lunarPhase = TestFactory.UseHeroPower(engine, 0);
            Assert.That(lunarPhase.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(PlayerId.Two).SpellDamageBonus, Is.EqualTo(1));
        }
    }
}
