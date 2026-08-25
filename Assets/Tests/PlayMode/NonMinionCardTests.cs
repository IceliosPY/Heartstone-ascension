using System.Collections;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Spells, on the board and in the hand.
    ///
    /// Until this phase a spell was refused for its type, because nothing could
    /// have happened when one resolved. It can now. What has not changed is
    /// where a spell goes: nothing appears on the board, and the card ends up in
    /// the graveyard.
    ///
    /// The Coin is the one worth watching. It works because of the data on it
    /// and nothing else, so playing it from the scene through the ordinary
    /// pointer is the honest test of that claim.
    /// </summary>
    public sealed class NonMinionCardTests : InteractionTestBase
    {
        private CardView FindSpellInHand()
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

        private IEnumerator AdvanceToTheHolder()
        {
            for (int guard = 0; guard < 4 && FindSpellInHand() == null; guard++)
            {
                yield return EndTurn();
            }

            Assert.That(FindSpellInHand(), Is.Not.Null, "Neither player is holding a spell.");
        }

        [UnityTest]
        public IEnumerator A_spell_can_be_read_in_hand()
        {
            yield return LoadMatch();
            yield return AdvanceToTheHolder();

            CardView spell = FindSpellInHand();

            MoveTo(spell.transform.position);

            Assert.That(spell.IsHovered, Is.True,
                "A spell could not be inspected. The pointer landed on " + Input.LastHit + ".");

            Vector3 resting = spell.RestingLocalPosition;
            yield return WaitUntil(() => spell.transform.localPosition.y > resting.y + 0.1f);

            Assert.That(spell.transform.localPosition.y, Is.GreaterThan(resting.y + 0.1f));
        }

        /// <summary>
        /// The Coin, played from the scene by dragging it. Three spendable mana
        /// from two crystals, and the crystals stay at two.
        /// </summary>
        [UnityTest]
        public IEnumerator The_coin_is_played_by_dragging_it_and_grants_a_temporary_mana()
        {
            yield return LoadWithScenario(DebugScenarios.CoinId);

            PlayerId acting = Session.State.CurrentPlayer;
            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.AvailableMana, Is.EqualTo(2));
            Assert.That(player.MaxMana, Is.EqualTo(2));

            CardView coin = FindCardInHand(DebugScenarios.TheCoin);
            Assert.That(coin, Is.Not.Null, "The coin scenario should deal The Coin.");
            Assert.That(coin.IsPlayable, Is.True, "The Coin costs nothing and should read as playable.");

            EntityId id = coin.EntityId;

            Drag(coin.transform.position, NearBoardRight);
            yield return Settle();

            Assert.That(player.AvailableMana, Is.EqualTo(3), "The Coin gave no mana.");
            Assert.That(player.MaxMana, Is.EqualTo(2), "The Coin granted a crystal.");
            Assert.That(player.TemporaryMana, Is.EqualTo(1));

            Assert.That(player.Board.Count, Is.Zero, "A spell put something on the board.");
            Assert.That(Presenter.TryGetCardView(id, out CardView _), Is.False,
                "The played spell is still shown in hand.");
        }

        [UnityTest]
        public IEnumerator A_spell_goes_to_the_graveyard_rather_than_the_board()
        {
            yield return LoadWithScenario(DebugScenarios.CoinId);

            PlayerId acting = Session.State.CurrentPlayer;
            Player player = Session.State.GetPlayer(acting);

            CardView coin = FindCardInHand(DebugScenarios.TheCoin);
            EntityId id = coin.EntityId;

            int graveyardBefore = player.Graveyard.Count;

            Session.Submit(new PlayCardCommand(acting, id));
            yield return Settle();

            Assert.That(player.Graveyard.Count, Is.EqualTo(graveyardBefore + 1));
            Assert.That(player.Board.Count, Is.Zero);
        }

        /// <summary>
        /// An unaffordable card is still readable. Not affording something stops
        /// it being played, not inspected.
        /// </summary>
        [UnityTest]
        public IEnumerator An_unaffordable_card_can_still_be_hovered()
        {
            yield return LoadMatch();

            CardView card = FirstCardInHand();

            // Turn one, one crystal: at least one card in hand is out of reach.
            MoveTo(card.transform.position);

            Assert.That(card.IsHovered, Is.True);
        }
    }
}
