using System.Collections;
using System.Collections.Generic;
using System.Text;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
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
    /// Playing a card takes two clicks: one on the card, one on your half of
    /// the board. These tests drive both of them through the real routing.
    ///
    /// They exist because everything around that routing was tested and the
    /// routing itself was not, which let a build in which no card could ever be
    /// played pass its whole suite twice. Asking the engine whether a command
    /// would be accepted proves nothing about whether a player can send it.
    /// </summary>
    public sealed class ClickPathTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        private GameSession _session;
        private MatchPresenter _presenter;
        private MatchInputController _input;
        private Camera _camera;

        private IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            MatchTestScene.MakeInstant();
            yield return null;

            _session = Object.FindFirstObjectByType<GameSession>();
            _presenter = Object.FindFirstObjectByType<MatchPresenter>();
            _input = Object.FindFirstObjectByType<MatchInputController>();
            _camera = Camera.main;

            Assert.That(_session, Is.Not.Null, "No GameSession in the match scene.");
            Assert.That(_presenter, Is.Not.Null, "No MatchPresenter in the match scene.");
            Assert.That(_input, Is.Not.Null, "No MatchInputController in the match scene.");
            Assert.That(_camera, Is.Not.Null, "No main camera in the match scene.");
        }

        private IEnumerator Settle()
        {
            while (_session.IsBusy)
            {
                yield return null;
            }

            yield return null;
        }

        private IEnumerator EndTurn()
        {
            _session.Submit(new EndTurnCommand(_session.State.CurrentPlayer));
            yield return Settle();
        }

        /// <summary>A click aimed at a point in the world, as the mouse makes one.</summary>
        private void ClickAt(Vector3 worldPoint)
        {
            Vector3 origin = _camera.transform.position;
            _input.HandleClick(new Ray(origin, (worldPoint - origin).normalized));
        }

        /// <summary>
        /// Somewhere on the acting side of the board that no minion stands on,
        /// so the click lands on the drop zone itself.
        /// </summary>
        private static Vector3 EmptyNearBoardSpot => new Vector3(4.5f, 0.2f, -1.05f);

        private List<CardView> PlayableCardsOfActivePlayer()
        {
            List<CardView> playable = new List<CardView>();
            Player active = _session.State.GetPlayer(_session.State.CurrentPlayer);

            foreach (CardInstance card in active.Hand)
            {
                // A plain minion: the hand now also holds spells and cards that
                // want a target first, and this file is about the two clicks
                // that put a body on the board.
                if (_session.State.Catalog.Get(card.CardId).Type != CardType.Minion)
                {
                    continue;
                }

                if (_session.GetPlayTargetRequirement(active.Id, card.Id) != PlayTargetRequirement.None &&
                    _session.GetLegalPlayTargets(active.Id, card.Id).Count > 0)
                {
                    continue;
                }

                if (_presenter.TryGetCardView(card.Id, out CardView view) && view.IsPlayable)
                {
                    playable.Add(view);
                }
            }

            return playable;
        }

        /// <summary>
        /// Clicks a card, then the board, and checks a minion arrived. Reports
        /// which of the two clicks went wrong rather than only that the board
        /// stayed empty.
        /// </summary>
        private IEnumerator PlayOneCardByClicking(string who)
        {
            PlayerId acting = _session.State.CurrentPlayer;
            int before = _session.State.GetPlayer(acting).Board.Count;

            List<CardView> playable = PlayableCardsOfActivePlayer();
            Assert.That(playable, Is.Not.Empty, who + ": nothing in hand is playable.");

            CardView card = playable[0];
            EntityId cardId = card.EntityId;

            ClickAt(card.transform.position);

            Assert.That(_input.HasSelection, Is.True,
                who + ": clicking the card selected nothing. The click landed on " + _input.LastHit + ".");
            Assert.That(_input.SelectedEntity, Is.EqualTo(cardId),
                who + ": clicking the card selected something else. The click landed on " + _input.LastHit + ".");

            ClickAt(EmptyNearBoardSpot);

            Assert.That(_input.HasSelection, Is.False,
                who + ": clicking the board left the card selected. The click landed on " + _input.LastHit + ".");

            yield return Settle();

            Assert.That(_session.State.GetPlayer(acting).Board.Count, Is.EqualTo(before + 1),
                who + ": clicking the card then the board summoned nothing." +
                " The second click landed on " + _input.LastHit + ", which has to carry" +
                " a BoardDropZone marked as the near side for the play to go through.");

            Assert.That(_presenter.TryGetCardView(cardId, out CardView _), Is.False,
                who + ": the played card is still shown in hand.");
        }

        /// <summary>
        /// The whole reason for this file: click a card, click your board, get a
        /// minion. For both players, in turn.
        /// </summary>
        [UnityTest]
        public IEnumerator A_card_is_played_by_clicking_it_then_the_board()
        {
            yield return LoadMatch();

            PlayerId first = _session.State.StartingPlayer;

            // One crystal each for the first two turns, and Test Soldier costs
            // two, so nobody can act before the third.
            yield return EndTurn();
            yield return EndTurn();

            Assert.That(_session.State.CurrentPlayer, Is.EqualTo(first));
            yield return PlayOneCardByClicking("starting player");

            yield return EndTurn();
            Assert.That(_session.State.CurrentPlayer, Is.EqualTo(first.Opponent));
            yield return PlayOneCardByClicking("second player");

            yield return EndTurn();
            Assert.That(_session.State.CurrentPlayer, Is.EqualTo(first));
            yield return PlayOneCardByClicking("starting player, second time");
        }

        /// <summary>
        /// Several turns each, because the board flips at every turn change and
        /// a click has to keep working after every flip.
        /// </summary>
        [UnityTest]
        public IEnumerator Cards_stay_clickable_across_many_turn_changes()
        {
            yield return LoadMatch();

            yield return EndTurn();
            yield return EndTurn();

            for (int round = 0; round < 6; round++)
            {
                yield return PlayOneCardByClicking("round " + round + ", " + _session.State.CurrentPlayer);
                yield return EndTurn();
            }
        }

        /// <summary>
        /// A full hand stops a drawn card being kept, not a held card being
        /// played. This is the exact state the bug was reported from: turn in
        /// the thirties, ten crystals, ten cards, an empty board.
        /// </summary>
        [UnityTest]
        public IEnumerator A_full_hand_does_not_stop_a_card_being_played()
        {
            yield return LoadMatch();

            while (_session.State.TurnNumber < 33 && !_session.State.HasEnded)
            {
                yield return EndTurn();
            }

            GameState state = _session.State;
            Player active = state.GetPlayer(state.CurrentPlayer);

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(active.Hand.Count, Is.EqualTo(10), "The reported state had a full hand.");
            Assert.That(active.MaxMana, Is.EqualTo(10));
            Assert.That(active.Board.Count, Is.EqualTo(0));
            Assert.That(_session.IsBusy, Is.False, "Input is still locked with nothing left to replay.");

            yield return PlayOneCardByClicking("full hand");
        }

        /// <summary>
        /// No component in the scene may come back missing.
        ///
        /// A component class sharing a file with another type has no script
        /// asset of its own, so a scene cannot store a reference to it: it saves
        /// one that resolves to nothing, and the component is silently absent at
        /// run time. The drop zones were lost that way, and neither the scene
        /// nor the suite said a word about it.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_component_in_the_scene_resolves_to_a_script()
        {
            yield return LoadMatch();

            StringBuilder broken = new StringBuilder();

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                {
                    Component[] components = node.GetComponents<Component>();

                    for (int index = 0; index < components.Length; index++)
                    {
                        if (components[index] == null)
                        {
                            broken.AppendLine("missing script on " + PathOf(node) + ", slot " + index);
                        }
                    }
                }
            }

            Assert.That(broken.Length, Is.Zero,
                "The match scene has missing scripts, so those components do nothing at run time:\n" + broken);
        }

        private static string PathOf(Transform node)
        {
            string path = node.name;

            while (node.parent != null)
            {
                node = node.parent;
                path = node.name + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// Both halves of the board have to be marked, or a card has nowhere to
        /// be dropped and clicking the board silently cancels the play.
        /// </summary>
        [UnityTest]
        public IEnumerator The_board_has_a_near_drop_zone_and_a_far_one()
        {
            yield return LoadMatch();

            BoardDropZone[] zones = Object.FindObjectsByType<BoardDropZone>(FindObjectsSortMode.None);

            Assert.That(zones.Length, Is.EqualTo(2),
                "Expected one drop zone per side, found " + zones.Length + ".");

            int near = 0;

            for (int index = 0; index < zones.Length; index++)
            {
                Assert.That(zones[index].GetComponent<Collider>(), Is.Not.Null,
                    zones[index].name + " has no collider, so no click can ever reach it.");

                if (zones[index].IsNearSide)
                {
                    near++;
                }
            }

            Assert.That(near, Is.EqualTo(1), "Exactly one drop zone has to be the near side.");
        }
    }
}
