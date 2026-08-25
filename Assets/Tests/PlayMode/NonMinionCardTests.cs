using System.Collections;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// The Coin is in the second player's hand and there is no spell system yet,
    /// so it can be looked at and cannot be played.
    ///
    /// What matters here is how that comes about. Nothing in the presentation
    /// knows what The Coin is: the engine refuses a card whose type it cannot
    /// put on a board, and the interaction simply never starts. The day spells
    /// work, this stops being true on its own.
    /// </summary>
    public sealed class NonMinionCardTests : InteractionTestBase
    {
        /// <summary>
        /// The card in the acting player's hand that is not a minion, found by
        /// asking the catalog rather than by looking for a name.
        /// </summary>
        private CardView FindNonMinionInHand()
        {
            foreach (CardInstance card in Active.Hand)
            {
                CardDefinition definition = Session.State.Catalog.Get(card.CardId);

                if (definition.Type != CardType.Minion &&
                    Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>Reaches the turn of whoever holds a non minion card.</summary>
        private IEnumerator AdvanceToTheHolder()
        {
            for (int guard = 0; guard < 4 && FindNonMinionInHand() == null; guard++)
            {
                yield return EndTurn();
            }

            Assert.That(FindNonMinionInHand(), Is.Not.Null,
                "Neither player is holding a non minion card, so there is nothing to check.");
        }

        [UnityTest]
        public IEnumerator A_card_that_cannot_be_played_yet_is_still_readable()
        {
            yield return LoadMatch();
            yield return AdvanceToTheHolder();

            CardView coin = FindNonMinionInHand();

            Assert.That(
                Session.Validate(new PlayCardCommand(Session.State.CurrentPlayer, coin.EntityId)),
                Is.EqualTo(RejectionReason.CardTypeNotPlayable),
                "The engine should refuse it for its type, not for its cost.");

            MoveTo(coin.transform.position);

            Assert.That(coin.IsHovered, Is.True,
                "It could not be inspected. The pointer landed on " + Input.LastHit + ".");

            Vector3 resting = coin.RestingLocalPosition;
            yield return WaitUntil(() => coin.transform.localPosition.y > resting.y + 0.1f);

            Assert.That(coin.transform.localPosition.y, Is.GreaterThan(resting.y + 0.1f),
                "It would not rise to be read.");
        }

        [UnityTest]
        public IEnumerator A_card_that_cannot_be_played_yet_produces_no_command()
        {
            yield return LoadMatch();
            yield return AdvanceToTheHolder();

            PlayerId acting = Session.State.CurrentPlayer;
            CardView coin = FindNonMinionInHand();
            EntityId id = coin.EntityId;

            int handBefore = Active.Hand.Count;
            int manaBefore = Active.AvailableMana;

            Drag(coin.transform.position, NearBoardRight);
            yield return Settle();

            Player player = Session.State.GetPlayer(acting);

            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.DraggingHandCard),
                "It was picked up.");
            Assert.That(player.Board.Count, Is.Zero, "Something reached the board.");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore), "It left the hand.");
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore), "It cost mana anyway.");
            Assert.That(Presenter.TryGetCardView(id, out CardView _), Is.True, "It lost its view.");
        }
    }
}
