using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Server;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// What a client is allowed to ask the rules.
    ///
    /// Everything above <see cref="IGameServer"/> talks to the match through
    /// this and nothing else, so what it answers has to be exactly what the
    /// engine would answer. A client that gets a different reply here would end
    /// up showing a board the rules disagree with, and a networked client would
    /// do it from another machine.
    /// </summary>
    public sealed class GameServerSeamTests
    {
        private static LocalGameServer StartedServer(out DeckList deck)
        {
            CardCatalog catalog = TestFactory.Catalog(
                TestFactory.MinionDefinition(manaCost: 2, attack: 2, health: 3),
                TestFactory.CoinDefinition());

            deck = TestFactory.Deck();

            LocalGameServer server = new LocalGameServer(new GameConfig(), catalog, seed: 7UL);
            server.StartMatch(deck, deck);
            return server;
        }

        /// <summary>Gets both players past the mulligan and into a real turn.</summary>
        private static IGameServer PlayingServer()
        {
            LocalGameServer server = StartedServer(out DeckList _);

            server.Execute(new MulliganCommand(PlayerId.One));
            server.Execute(new MulliganCommand(PlayerId.Two));

            return server;
        }

        [Test]
        public void The_seam_reports_a_minion_that_cannot_attack_and_says_why()
        {
            IGameServer server = PlayingServer();

            PlayerId acting = server.State.CurrentPlayer;

            // Nothing on the board, so there is no such attacker at all.
            Assert.That(
                server.CanAttack(acting, new EntityId(9999)),
                Is.EqualTo(RejectionReason.InvalidAttacker));
        }

        [Test]
        public void The_seam_refuses_an_attack_for_the_player_whose_turn_it_is_not()
        {
            IGameServer server = PlayingServer();

            PlayerId waiting = server.State.CurrentPlayer.Opponent;

            Assert.That(
                server.CanAttack(waiting, new EntityId(1)),
                Is.EqualTo(RejectionReason.NotYourTurn));
        }

        [Test]
        public void A_minion_summoned_this_turn_is_reported_as_summoning_sick()
        {
            IGameServer server = PlayingServer();

            PlayerId acting = server.State.CurrentPlayer;
            Minion minion = FirstMinionAfterPlaying(server, acting);

            Assert.That(
                server.CanAttack(acting, minion.Id),
                Is.EqualTo(RejectionReason.SummoningSickness));

            // And a sick minion is offered no targets either, so a client that
            // only asks for targets reaches the same conclusion.
            Assert.That(server.GetLegalAttackTargets(acting, minion.Id), Is.Empty);
        }

        [Test]
        public void A_rested_minion_is_reported_as_able_to_attack()
        {
            IGameServer server = PlayingServer();

            PlayerId acting = server.State.CurrentPlayer;
            Minion minion = FirstMinionAfterPlaying(server, acting);

            // Round trip: their next turn.
            server.Execute(new EndTurnCommand(acting));
            server.Execute(new EndTurnCommand(server.State.CurrentPlayer));

            Assert.That(server.State.CurrentPlayer, Is.EqualTo(acting));
            Assert.That(server.CanAttack(acting, minion.Id), Is.EqualTo(RejectionReason.None));
            Assert.That(server.GetLegalAttackTargets(acting, minion.Id), Is.Not.Empty);
        }

        /// <summary>
        /// Asking changes nothing. A client may ask as often as it likes, every
        /// frame if it wants, and the match must be exactly where it was.
        /// </summary>
        [Test]
        public void Asking_the_seam_a_question_never_changes_the_match()
        {
            IGameServer server = PlayingServer();

            PlayerId acting = server.State.CurrentPlayer;
            Minion minion = FirstMinionAfterPlaying(server, acting);

            int entities = server.State.EntityCount;
            int turn = server.State.TurnNumber;
            int mana = server.State.GetPlayer(acting).AvailableMana;
            int board = server.State.GetPlayer(acting).Board.Count;

            for (int repeat = 0; repeat < 50; repeat++)
            {
                server.CanAttack(acting, minion.Id);
                server.GetLegalAttackTargets(acting, minion.Id);
                server.CanExecute(new EndTurnCommand(acting));
            }

            Assert.That(server.State.EntityCount, Is.EqualTo(entities));
            Assert.That(server.State.TurnNumber, Is.EqualTo(turn));
            Assert.That(server.State.GetPlayer(acting).AvailableMana, Is.EqualTo(mana));
            Assert.That(server.State.GetPlayer(acting).Board.Count, Is.EqualTo(board));
        }

        private static Minion FirstMinionAfterPlaying(IGameServer server, PlayerId acting)
        {
            // Enough turns for Test Soldier to be affordable.
            while (server.State.GetPlayer(acting).MaxMana < 2 || server.State.CurrentPlayer != acting)
            {
                server.Execute(new EndTurnCommand(server.State.CurrentPlayer));
            }

            Player player = server.State.GetPlayer(acting);

            foreach (CardInstance card in player.Hand)
            {
                if (server.CanExecute(new PlayCardCommand(acting, card.Id)) == RejectionReason.None)
                {
                    server.Execute(new PlayCardCommand(acting, card.Id));
                    break;
                }
            }

            Assert.That(player.Board.Count, Is.GreaterThan(0), "Nothing could be played.");
            return player.Board[0];
        }
    }
}
