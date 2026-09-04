using System.Collections;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Huntress Shot as it actually shows up and plays in the real match
    /// scene: an ordinary Spell <see cref="CardView"/>, aimed the same way
    /// every other targeted card already is, with no card-specific
    /// Presentation code anywhere in the path.
    /// </summary>
    public sealed class HuntressShotTests : InteractionTestBase
    {
        private const string CardId = "starcaller_huntress_shot";

        private Player Two() => Session.State.GetPlayer(PlayerId.Two);

        private CardView HuntressShotInHand()
        {
            foreach (CardInstance card in Two().Hand)
            {
                if (card.CardId.Value == CardId && Presenter.TryGetCardView(card.Id, out CardView view))
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

        // ------------------------------------------------------------------
        //  Renders as a normal Spell CardView
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Huntress_shot_renders_as_an_ordinary_spell_card_view()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();

            Assert.That(view, Is.Not.Null, "Huntress Shot is not in player two's hand.");
            Assert.That(view.IsFaceDown, Is.False, "The active player's own card must be shown face up.");
            Assert.That(view.Shown.Name, Is.EqualTo("Huntress Shot"));
        }

        [UnityTest]
        public IEnumerator The_printed_cost_is_three()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.Shown.ManaCost, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator The_card_visual_library_resolves_the_final_production_artwork()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();
            Assert.That(view, Is.Not.Null);

            Sprite artwork = view.Plan.SpriteIn(CardVisualSlot.Artwork);

            Assert.That(artwork, Is.Not.Null, "Huntress Shot drew no artwork at all.");
            Assert.That(artwork.name, Is.EqualTo("Huntress_Shot"),
                "Huntress Shot resolved '" + artwork.name + "' instead of its own final artwork - " +
                "and not through any card-specific rendering branch, since none exists.");
        }

        /// <summary>
        /// No dedicated Starcaller Spell frame exists yet (see the final
        /// report) - this only proves the generic catalog resolution still
        /// gives Huntress Shot a complete frame rather than nothing, through
        /// the same fallback path any Spell with no class-specific entry
        /// already uses.
        /// </summary>
        [UnityTest]
        public IEnumerator A_frame_is_resolved_even_without_a_dedicated_starcaller_spell_frame()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();
            Assert.That(view, Is.Not.Null);

            Sprite frame = view.Plan.SpriteIn(CardVisualSlot.Frame);

            Assert.That(frame, Is.Not.Null, "Huntress Shot drew no frame at all.");
        }

        // ------------------------------------------------------------------
        //  Targeting - the existing generic architecture, unmodified
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Targeting_requires_a_minion_and_offers_both_sides()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();
            Assert.That(view, Is.Not.Null);

            Assert.That(Session.GetPlayTargetRequirement(PlayerId.Two, view.EntityId),
                Is.EqualTo(PlayTargetRequirement.Required));

            var legal = Session.GetLegalPlayTargets(PlayerId.Two, view.EntityId);

            Assert.That(legal, Has.Member(Two().Board[0].Id), "The friendly minion should be aimable.");
            Assert.That(legal, Has.Member(Session.State.GetPlayer(PlayerId.One).Board[0].Id),
                "The enemy minion should be aimable.");
            Assert.That(legal, Has.No.Member(Two().Hero.Id), "The friendly hero must never be aimable.");
            Assert.That(legal, Has.No.Member(Session.State.GetPlayer(PlayerId.One).Hero.Id),
                "The enemy hero must never be aimable.");
        }

        [UnityTest]
        public IEnumerator Clicking_it_down_and_aiming_at_the_enemy_minion_resolves_it()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView card = HuntressShotInHand();
            Assert.That(card, Is.Not.Null);

            EntityId targetId = Session.State.GetPlayer(PlayerId.One).Board[0].Id;
            Assert.That(Presenter.TryGetMinionView(targetId, out MinionView victim), Is.True);

            int before = Session.State.GetPlayer(PlayerId.One).Board[0].CurrentHealth;

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "Huntress Shot must ask for a target through the same generic path every other " +
                "targeted card uses.");

            CarryTo(victim.transform.position);
            Click(victim.transform.position);
            yield return Settle();

            Assert.That(before - Session.State.GetPlayer(PlayerId.One).Board[0].CurrentHealth, Is.EqualTo(1),
                "1 damage, no Spell Damage active.");
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
        }

        // ------------------------------------------------------------------
        //  Generic Spell Damage text display - no card-specific code
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Without_spell_damage_the_hand_card_shows_its_printed_amount()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            CardView view = HuntressShotInHand();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.Shown.RulesText, Is.EqualTo(
                "Deal 1 damage to a minion.\nRestore 1 Mana for each Spell Damage you have."));
        }

        [UnityTest]
        public IEnumerator With_plus_one_spell_damage_the_displayed_damage_becomes_buffed_two()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));
            Assert.That(Two().Hero.HeroPowerCardId.Value, Is.EqualTo("starcaller_lunar_phase"));

            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0)), Is.True);
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().SpellDamageBonus, Is.EqualTo(1));

            CardView view = HuntressShotInHand();
            Assert.That(view, Is.Not.Null);

            string shown = view.Shown.RulesText;

            Assert.That(shown, Does.Contain("*2*"),
                "Base 1 damage plus Lunar Phase's +1 must display as *2*.");
            Assert.That(shown, Does.Contain("<color=" + SpellDamageTextFormatter.HighlightColorHex + ">*2*</color>"),
                "The boosted damage must use the existing buffed/green formatting, not a new one.");
            Assert.That(shown, Does.Not.Contain("1 damage"),
                "The base printed damage must not still be visible once boosted.");
        }

        /// <summary>
        /// The exact regression the brief calls out by name: Spell Damage
        /// must only ever touch the DealDamage number. Huntress Shot's own
        /// "1 Mana for each Spell Damage" line is never itself a DealDamage
        /// action, so the generic formatter must leave it alone even while
        /// it is rewriting the damage line right next to it.
        /// </summary>
        [UnityTest]
        public IEnumerator The_mana_restoration_wording_is_never_numerically_altered()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0));
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().SpellDamageBonus, Is.EqualTo(1));

            CardView view = HuntressShotInHand();
            Assert.That(view, Is.Not.Null);

            string shown = view.Shown.RulesText;

            Assert.That(shown, Does.Contain("Restore 1 Mana for each Spell Damage you have."),
                "The mana restoration line must stay exactly as printed, even with Spell Damage active.");
            Assert.That(shown, Does.Not.Contain("Restore 2 Mana"));
        }

        /// <summary>
        /// The catalog's own definition - the source of truth every other
        /// card's display reads from - is never mutated by any of the above.
        /// </summary>
        [UnityTest]
        public IEnumerator The_cards_own_authored_text_is_never_mutated()
        {
            yield return LoadWithScenario(DebugScenarios.HuntressShotDisplayId);

            Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0));
            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(Session.State.Catalog.TryGet(new CardId(CardId), out var definition), Is.True);
            Assert.That(definition.Text, Is.EqualTo(
                "Deal 1 damage to a minion.\nRestore 1 Mana for each Spell Damage you have."),
                "The catalog's own card definition must never be rewritten by a display concern.");
        }
    }
}
