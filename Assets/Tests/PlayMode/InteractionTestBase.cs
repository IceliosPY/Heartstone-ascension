using System.Collections;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Shared ground for the interaction tests: the scene, the pointer, and the
    /// few gestures every one of them is built from.
    ///
    /// The gestures go through the same three entry points a mouse does. A
    /// pointer device does not exist in batch mode, so what is left uncovered is
    /// reading a screen position off the device and turning it into a ray, which
    /// is the three lines at the top of the controller. Everything after that is
    /// the code the player drives.
    /// </summary>
    public abstract class InteractionTestBase
    {
        protected const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        protected GameSession Session;
        protected MatchPresenter Presenter;
        protected MatchInputController Input;
        protected Camera MatchCamera;

        protected IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            MatchTestScene.MakeInstant();
            yield return null;

            Session = Object.FindFirstObjectByType<GameSession>();
            Presenter = Object.FindFirstObjectByType<MatchPresenter>();
            Input = Object.FindFirstObjectByType<MatchInputController>();
            MatchCamera = Camera.main;

            Assert.That(Session, Is.Not.Null, "No GameSession in the match scene.");
            Assert.That(Presenter, Is.Not.Null, "No MatchPresenter in the match scene.");
            Assert.That(Input, Is.Not.Null, "No MatchInputController in the match scene.");
            Assert.That(MatchCamera, Is.Not.Null, "No main camera in the match scene.");
        }

        /// <summary>
        /// Loads the match and drops it straight into a prepared position.
        ///
        /// Which is what the debug scenarios are for: reaching a situation with
        /// a particular card in a particular hand takes several turns and a
        /// certain amount of luck, and none of that is what these tests are
        /// about.
        /// </summary>
        protected IEnumerator LoadWithScenario(string scenarioId)
        {
            yield return LoadMatch();

            CoH.App.MatchDebugTools tools = Object.FindFirstObjectByType<CoH.App.MatchDebugTools>();

            Assert.That(tools, Is.Not.Null, "The scene has no MatchDebugTools.");
            Assert.That(tools.LoadScenario(scenarioId), Is.True,
                "The scenario '" + scenarioId + "' could not be loaded.");

            yield return null;
        }

        protected IEnumerator Settle()
        {
            while (Session.IsBusy)
            {
                yield return null;
            }

            yield return null;
        }

        protected IEnumerator EndTurn()
        {
            Session.Submit(new EndTurnCommand(Session.State.CurrentPlayer));
            yield return Settle();
        }

        /// <summary>Two turns, so the same player is acting again.</summary>
        protected IEnumerator RoundTrip()
        {
            yield return EndTurn();
            yield return EndTurn();
        }

        /// <summary>
        /// Waits for something to become true, on a clock rather than on a
        /// frame count.
        ///
        /// Poses are eased by elapsed time, and a batch mode frame covers far
        /// less of it than a rendered one. Counting frames would make these
        /// tests measure how fast the machine is rather than whether the card
        /// arrives.
        /// </summary>
        protected IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 4f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;

            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        // --- gestures ------------------------------------------------------

        protected Ray RayTo(Vector3 worldPoint)
        {
            Vector3 origin = MatchCamera.transform.position;
            return new Ray(origin, (worldPoint - origin).normalized);
        }

        protected void Press(Vector3 worldPoint) => Input.PointerDown(RayTo(worldPoint));

        protected void MoveTo(Vector3 worldPoint) => Input.PointerMove(RayTo(worldPoint));

        protected void Release(Vector3 worldPoint) => Input.PointerUp(RayTo(worldPoint));

        /// <summary>Press, move, release: the whole drag a player performs.</summary>
        protected void Drag(Vector3 from, Vector3 to)
        {
            Press(from);
            MoveTo(to);
            Release(to);
        }

        /// <summary>
        /// A whole mouse click: the button goes down and comes back up in the
        /// same place.
        ///
        /// Both halves, always. A press on its own is half a gesture, and a
        /// controller that does the right thing on the press can still undo it
        /// on the release nobody sent it. That is not hypothetical; it is the
        /// shape of a bug this suite let through once.
        /// </summary>
        protected void Click(Vector3 worldPoint)
        {
            Press(worldPoint);
            Release(worldPoint);
        }

        /// <summary>
        /// Moves the pointer the way frames do, in several steps rather than
        /// one jump, so anything that measures how far a gesture has travelled
        /// sees a journey rather than a teleport.
        /// </summary>
        protected void CarryTo(Vector3 worldPoint)
        {
            for (int step = 0; step < 3; step++)
            {
                MoveTo(worldPoint);
            }
        }

        // --- places on the board -------------------------------------------

        /// <summary>Over the acting player's row, clear of any minion on it.</summary>
        protected static Vector3 NearBoardRight => new Vector3(4.5f, 0.2f, -1.05f);

        /// <summary>Well off the table, where a release means "never mind".</summary>
        protected static Vector3 EmptySpace => new Vector3(24f, 0.45f, -1.05f);

        /// <summary>A point over the acting player's row at a chosen offset.</summary>
        protected Vector3 NearBoardAt(float localX)
        {
            Transform row = Presenter.NearBoardAnchor;
            Vector3 world = row.TransformPoint(new Vector3(localX, 0f, 0f));
            return new Vector3(world.x, 0.2f, world.z);
        }

        // --- reading the match ---------------------------------------------

        protected Player Active => Session.State.GetPlayer(Session.State.CurrentPlayer);

        /// <summary>
        /// The cards that can be dropped straight onto the board: minions that
        /// are affordable and are not waiting to be aimed.
        ///
        /// Narrower than "playable" on purpose. Almost every test that asks for
        /// a playable card means "something I can put on the board and then look
        /// at", and a hand now also holds spells and cards that want a target
        /// first. Those have tests of their own.
        /// </summary>
        protected List<CardView> PlayableCards()
        {
            List<CardView> playable = new List<CardView>();
            PlayerId acting = Session.State.CurrentPlayer;

            foreach (CardInstance card in Active.Hand)
            {
                if (!Presenter.TryGetCardView(card.Id, out CardView view) || !view.IsPlayable)
                {
                    continue;
                }

                if (Session.State.Catalog.Get(card.CardId).Type != CardType.Minion)
                {
                    continue;
                }

                if (Session.GetPlayTargetRequirement(acting, card.Id) != PlayTargetRequirement.None &&
                    Session.GetLegalPlayTargets(acting, card.Id).Count > 0)
                {
                    continue;
                }

                playable.Add(view);
            }

            return playable;
        }

        /// <summary>Every playable card, spells and targeted cards included.</summary>
        protected List<CardView> PlayableCardsOfAnyKind()
        {
            List<CardView> playable = new List<CardView>();

            foreach (CardInstance card in Active.Hand)
            {
                if (Presenter.TryGetCardView(card.Id, out CardView view) && view.IsPlayable)
                {
                    playable.Add(view);
                }
            }

            return playable;
        }

        /// <summary>The first card in hand of a named kind, or null.</summary>
        protected CardView FindCardInHand(string cardId)
        {
            foreach (CardInstance card in Active.Hand)
            {
                if (string.Equals(card.CardId.Value, cardId, System.StringComparison.Ordinal) &&
                    Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>
        /// Puts a named card into the acting player's hand through the debug
        /// scenario path, so a test can reach a card the shuffle did not give it.
        /// </summary>
        protected bool HandHolds(string cardId) => FindCardInHand(cardId) != null;

        protected CardView FirstPlayableCard()
        {
            List<CardView> playable = PlayableCards();
            Assert.That(playable, Is.Not.Empty, "The acting player has nothing playable.");
            return playable[0];
        }

        protected CardView FirstCardInHand()
        {
            foreach (CardInstance card in Active.Hand)
            {
                if (Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    return view;
                }
            }

            Assert.Fail("The acting player has no card views at all.");
            return null;
        }

        /// <summary>Reaches a turn where Test Soldier is affordable.</summary>
        protected IEnumerator AdvanceUntilSomethingIsPlayable()
        {
            for (int guard = 0; guard < 12 && PlayableCards().Count == 0; guard++)
            {
                yield return EndTurn();
            }

            Assert.That(PlayableCards(), Is.Not.Empty, "Nobody ever reached two mana.");
        }

        /// <summary>
        /// Plays minions for the acting player until their row is full, using
        /// the engine directly. This is setup, not the thing under test.
        /// </summary>
        protected IEnumerator FillActiveBoard()
        {
            PlayerId seat = Session.State.CurrentPlayer;

            for (int guard = 0; guard < 60; guard++)
            {
                if (Session.State.GetPlayer(seat).Board.Count >= 7)
                {
                    yield break;
                }

                bool playedSomething = false;

                foreach (CardInstance card in Session.State.GetPlayer(seat).Hand)
                {
                    if (Session.State.CurrentPlayer != seat)
                    {
                        break;
                    }

                    // A plain minion, so filling a board never spends a turn on
                    // a spell or stalls on a card waiting to be aimed.
                    if (Session.State.Catalog.Get(card.CardId).Type != CardType.Minion)
                    {
                        continue;
                    }

                    if (Session.CanSubmit(new PlayCardCommand(seat, card.Id)))
                    {
                        Session.Submit(new PlayCardCommand(seat, card.Id));
                        yield return Settle();
                        playedSomething = true;
                        break;
                    }
                }

                if (!playedSomething)
                {
                    yield return RoundTrip();
                }
            }

            Assert.Fail("The board never filled up.");
        }

        /// <summary>Plays one minion for the acting player, through the engine.</summary>
        protected IEnumerator PlayOneMinionDirectly()
        {
            PlayerId seat = Session.State.CurrentPlayer;
            CardView card = FirstPlayableCard();

            Session.Submit(new PlayCardCommand(seat, card.EntityId));
            yield return Settle();
        }

        protected MinionView FirstMinionOf(PlayerId seat)
        {
            Player player = Session.State.GetPlayer(seat);
            Assert.That(player.Board.Count, Is.GreaterThan(0), "That player has no minion.");

            Assert.That(Presenter.TryGetMinionView(player.Board[0].Id, out MinionView view), Is.True,
                "The minion has no view.");

            return view;
        }

        protected HeroView HeroViewOf(PlayerId seat)
        {
            if (Presenter.NearHero != null && Presenter.NearHero.PlayerId == seat)
            {
                return Presenter.NearHero;
            }

            Assert.That(Presenter.FarHero, Is.Not.Null);
            Assert.That(Presenter.FarHero.PlayerId, Is.EqualTo(seat));
            return Presenter.FarHero;
        }
    }
}
