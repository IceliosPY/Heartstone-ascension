using System.Collections;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Lunar Phase's Spell Damage, as it actually shows up on a real hand
    /// card in the real match scene - not the arithmetic itself (proven in
    /// <c>SpellDamageTests</c>), only that the printed damage number on a
    /// damaging spell in Player 2's own hand visibly updates the instant
    /// the bonus is granted, and reverts the instant it expires.
    /// </summary>
    public sealed class SpellDamageDisplayTests : InteractionTestBase
    {
        private Player Two() => Session.State.GetPlayer(PlayerId.Two);

        private CardView FindTestAoeInHand()
        {
            foreach (CardInstance card in Two().Hand)
            {
                if (card.CardId.Value == DebugScenarios.TestAoe &&
                    Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    return view;
                }
            }

            return null;
        }

        private IEnumerator WaitUntilQueueIsIdle()
        {
            int guard = 0;

            while (Session.IsBusy && guard++ < 600)
            {
                yield return null;
            }

            Assert.That(Session.IsBusy, Is.False, "The presentation queue never finished.");
        }

        [UnityTest]
        public IEnumerator Without_spell_damage_the_hand_card_shows_its_printed_amount()
        {
            yield return LoadWithScenario(DebugScenarios.SpellDamageDisplayId);

            CardView view = FindTestAoeInHand();

            Assert.That(view, Is.Not.Null, "Test Volley is not in player two's hand.");
            Assert.That(view.Shown.RulesText, Is.EqualTo("Deal 1 damage to all enemy minions."));
        }

        [UnityTest]
        public IEnumerator Using_lunar_phase_immediately_shows_the_boosted_damage_in_green()
        {
            yield return LoadWithScenario(DebugScenarios.SpellDamageDisplayId);

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));
            Assert.That(Two().Hero.HeroPowerCardId.Value, Is.EqualTo("starcaller_lunar_phase"));

            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0)), Is.True);
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().SpellDamageBonus, Is.EqualTo(1));

            CardView view = FindTestAoeInHand();
            Assert.That(view, Is.Not.Null);

            string shown = view.Shown.RulesText;

            Assert.That(shown, Does.Contain("*2*"),
                "Base 1 damage plus Lunar Phase's +1 must display as *2*.");
            Assert.That(shown, Does.Contain("<color=" + SpellDamageTextFormatter.HighlightColorHex + ">*2*</color>"));
            Assert.That(shown, Does.Not.Contain("1 damage"),
                "The base printed amount must not still be visible once boosted.");
        }

        [UnityTest]
        public IEnumerator Ending_the_turn_reverts_the_display_to_the_printed_amount()
        {
            yield return LoadWithScenario(DebugScenarios.SpellDamageDisplayId);

            Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0));
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(FindTestAoeInHand().Shown.RulesText, Does.Contain("*2*"),
                "Sanity check: the bonus must actually be showing before we test it going away.");

            Session.Submit(new EndTurnCommand(PlayerId.Two));
            yield return null;
            yield return HandAtRest();
            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().SpellDamageBonus, Is.Zero,
                "The bonus itself must be gone the instant player two's turn ends.");

            // Player two's hand is now the far, face-down hand - it shows no
            // rules text at all regardless of Spell Damage, by design. Coming
            // back around to player two's own turn is what makes the card
            // face-up and readable again, which is the real moment a player
            // would actually see the reverted text.
            Session.Submit(new EndTurnCommand(PlayerId.One));
            yield return null;
            yield return HandAtRest();
            yield return WaitUntilQueueIsIdle();

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));

            CardView view = FindTestAoeInHand();
            Assert.That(view, Is.Not.Null, "Test Volley should still be in player two's hand after ending the turn.");
            Assert.That(view.Shown.RulesText, Is.EqualTo("Deal 1 damage to all enemy minions."),
                "The display must revert to the printed amount the instant the bonus expires.");
        }

        /// <summary>
        /// The one thing this whole feature is not allowed to do, checked
        /// directly against the catalog: the authored card's own text is
        /// read fresh from the real match catalog after Lunar Phase has
        /// been used, and must be exactly what was authored.
        /// </summary>
        [UnityTest]
        public IEnumerator The_cards_own_authored_text_is_never_mutated()
        {
            yield return LoadWithScenario(DebugScenarios.SpellDamageDisplayId);

            Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0));
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().SpellDamageBonus, Is.EqualTo(1));

            Assert.That(Session.State.Catalog.TryGet(new CardId(DebugScenarios.TestAoe), out CardDefinition definition),
                Is.True);
            Assert.That(definition.Text, Is.EqualTo("Deal 1 damage to all enemy minions."),
                "The catalog's own card definition must never be rewritten by a display concern.");
        }
    }
}
