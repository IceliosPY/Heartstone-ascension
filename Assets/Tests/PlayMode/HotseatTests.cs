using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Hotseat: both players share one screen, and both must be able to play.
    ///
    /// These tests exist because the first playable build let player one act
    /// and left player two stuck. The command path was identical for both, so
    /// asserting that a command is accepted proves nothing: what broke was the
    /// presentation, which parked the second player's hand somewhere they could
    /// not reach. So these tests check reachability, not just acceptance.
    /// </summary>
    public sealed class HotseatTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        private static IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            MatchTestScene.MakeInstant();
            yield return null;
        }

        private static IEnumerator Settle(GameSession session)
        {
            while (session.IsBusy)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator EndTurn(GameSession session)
        {
            session.Submit(new EndTurnCommand(session.State.CurrentPlayer));
            yield return Settle(session);
        }

        /// <summary>
        /// The one thing a player actually needs: a card they can see and click.
        ///
        /// Checks the view sits inside the camera's view and that a ray from the
        /// camera reaches it first. Anything in front of it, a hero, another
        /// card, the table, makes it unclickable however correct the rules are.
        /// </summary>
        private static void AssertCardIsReachable(CardView view, string who)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);

            Vector3 target = view.transform.position;
            Vector3 viewport = camera.WorldToViewportPoint(target);

            Assert.That(viewport.z, Is.GreaterThan(0f), who + ": the card is behind the camera.");
            Assert.That(viewport.x, Is.InRange(0.02f, 0.98f), who + ": the card is off the side of the screen.");
            Assert.That(viewport.y, Is.InRange(0.02f, 0.98f), who + ": the card is off the top or bottom of the screen.");

            Vector3 origin = camera.transform.position;
            Ray ray = new Ray(origin, (target - origin).normalized);

            Assert.That(Physics.Raycast(ray, out RaycastHit hit, 200f), Is.True,
                who + ": nothing at all is hit where the card is.");

            CardView struck = hit.collider.GetComponentInParent<CardView>();

            Assert.That(struck, Is.SameAs(view),
                who + ": clicking hits " + hit.collider.name + " instead of the card." +
                " card at " + target.ToString("F2") +
                ", hit at " + hit.point.ToString("F2") +
                ", card distance " + Vector3.Distance(origin, target).ToString("F2") +
                ", hit distance " + hit.distance.ToString("F2"));
        }

        private static List<CardView> PlayableViewsOfActivePlayer(GameSession session, MatchPresenter presenter)
        {
            List<CardView> playable = new List<CardView>();
            Player active = session.State.GetPlayer(session.State.CurrentPlayer);

            foreach (CardInstance card in active.Hand)
            {
                if (presenter.TryGetCardView(card.Id, out CardView view) && view.IsPlayable)
                {
                    playable.Add(view);
                }
            }

            return playable;
        }

        [UnityTest]
        public IEnumerator Both_players_can_play_a_card_in_turn()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();

            PlayerId first = session.State.StartingPlayer;
            PlayerId second = first.Opponent;

            // Turns one and two: a single crystal each, and Test Soldier costs
            // two, so nobody can act yet.
            yield return EndTurn(session);
            yield return EndTurn(session);

            // --- The starting player's third turn -------------------------
            Assert.That(session.State.CurrentPlayer, Is.EqualTo(first));

            List<CardView> firstPlayerCards = PlayableViewsOfActivePlayer(session, presenter);
            Assert.That(firstPlayerCards, Is.Not.Empty, "The starting player has nothing playable at two mana.");
            AssertCardIsReachable(firstPlayerCards[0], "starting player");

            EntityId firstCard = firstPlayerCards[0].EntityId;
            Assert.That(session.Submit(new PlayCardCommand(first, firstCard)), Is.True);
            yield return Settle(session);

            Assert.That(session.State.GetPlayer(first).Board.Count, Is.EqualTo(1));

            // --- The other player's turn ----------------------------------
            yield return EndTurn(session);
            Assert.That(session.State.CurrentPlayer, Is.EqualTo(second));

            List<CardView> secondPlayerCards = PlayableViewsOfActivePlayer(session, presenter);
            Assert.That(secondPlayerCards, Is.Not.Empty,
                "The second player has no playable card view: their hand was never shown face up.");
            AssertCardIsReachable(secondPlayerCards[0], "second player");

            EntityId secondCard = secondPlayerCards[0].EntityId;
            Assert.That(session.Submit(new PlayCardCommand(second, secondCard)), Is.True,
                "The second player's play was refused.");
            yield return Settle(session);

            Assert.That(session.State.GetPlayer(second).Board.Count, Is.EqualTo(1),
                "The second player's minion never reached the board.");
            Assert.That(presenter.TryGetCardView(secondCard, out CardView _), Is.False,
                "The played card is still shown in hand.");

            // --- And back again -------------------------------------------
            yield return EndTurn(session);
            Assert.That(session.State.CurrentPlayer, Is.EqualTo(first));

            List<CardView> againCards = PlayableViewsOfActivePlayer(session, presenter);
            Assert.That(againCards, Is.Not.Empty, "The starting player cannot act on their next turn.");
            AssertCardIsReachable(againCards[0], "starting player, second time");
        }

        [UnityTest]
        public IEnumerator The_active_players_hand_is_always_the_one_in_front()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();

            // Whoever holds the turn, their cards must sit in the same place on
            // screen. Nothing in the presentation may treat one seat as the
            // permanent human.
            Vector3 firstPlayerHandCentre = ActiveHandCentre(session, presenter);

            yield return EndTurn(session);

            Vector3 secondPlayerHandCentre = ActiveHandCentre(session, presenter);

            Assert.That(
                Vector3.Distance(firstPlayerHandCentre, secondPlayerHandCentre),
                Is.LessThan(0.75f),
                "The two players' hands are shown in different places, so only one of them is comfortable to play.");
        }

        private static Vector3 ActiveHandCentre(GameSession session, MatchPresenter presenter)
        {
            Player active = session.State.GetPlayer(session.State.CurrentPlayer);
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (CardInstance card in active.Hand)
            {
                if (presenter.TryGetCardView(card.Id, out CardView view))
                {
                    sum += view.transform.position;
                    count++;
                }
            }

            Assert.That(count, Is.GreaterThan(0), "The active player has no card views at all.");
            return sum / count;
        }

        [UnityTest]
        public IEnumerator Every_card_of_the_active_hand_stays_on_screen()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();
            Camera camera = Camera.main;

            // Ten turns each, so a hand grows toward its cap and the fan has to
            // keep it inside the frame.
            for (int turn = 0; turn < 12; turn++)
            {
                Player active = session.State.GetPlayer(session.State.CurrentPlayer);

                foreach (CardInstance card in active.Hand)
                {
                    if (!presenter.TryGetCardView(card.Id, out CardView view))
                    {
                        continue;
                    }

                    Vector3 viewport = camera.WorldToViewportPoint(view.transform.position);

                    Assert.That(viewport.x, Is.InRange(0.02f, 0.98f),
                        "A card of a hand of " + active.Hand.Count + " is off the side of the screen.");
                    Assert.That(viewport.y, Is.InRange(0.02f, 0.98f),
                        "A card of a hand of " + active.Hand.Count + " is off the top or bottom.");
                }

                yield return EndTurn(session);
            }
        }
    }
}
