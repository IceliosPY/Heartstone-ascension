using System.Collections;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Starcaller as the player meets it: in the real match scene, on the
    /// real board, through the real hero power view - the same one Raise
    /// already uses.
    ///
    /// Every test here reaches Player 2 through the ordinary development
    /// match (<c>MatchBootstrap</c>'s own defaults), never by constructing
    /// a bespoke scene state, so a failure here is a failure pressing Play
    /// would actually show.
    /// </summary>
    public sealed class StarcallerBoardTests : InteractionTestBase
    {
        private HeroPowerView HeroPower => Presenter.NearHeroPower;

        private Player One() => Session.State.GetPlayer(PlayerId.One);

        private Player Two() => Session.State.GetPlayer(PlayerId.Two);

        /// <summary>Ends turns until player two is acting, so Lunar Phase is theirs to use.</summary>
        private IEnumerator ReachPlayerTwosTurn()
        {
            int guard = 0;

            while (Session.State.CurrentPlayer != PlayerId.Two && guard++ < 4)
            {
                Session.Submit(new EndTurnCommand(Session.State.CurrentPlayer));
                yield return null;
                yield return HandAtRest();
                yield return WaitUntilQueueIsIdle();
            }

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));
        }

        /// <summary>
        /// Player two's very first turn only grants their first mana
        /// crystal - one, same as player one's own opening turn - so Lunar
        /// Phase's two-mana cost genuinely cannot be paid yet. Tests that
        /// actually click it need a turn where that is no longer true,
        /// reached the ordinary way: playing turns forward rather than
        /// reaching into state to hand mana out.
        /// </summary>
        private IEnumerator ReachPlayerTwosTurnWithMana(int minimumMana)
        {
            yield return ReachPlayerTwosTurn();

            int guard = 0;

            while (Two().AvailableMana < minimumMana && guard++ < 6)
            {
                Session.Submit(new EndTurnCommand(PlayerId.Two));
                yield return null;
                yield return HandAtRest();
                yield return WaitUntilQueueIsIdle();
                yield return ReachPlayerTwosTurn();
            }

            yield return WaitUntilQueueIsIdle();

            Assert.That(Two().AvailableMana, Is.GreaterThanOrEqualTo(minimumMana),
                "Could not reach a player two turn with enough mana to use Lunar Phase.");
        }

        // ==================================================================
        //  The development match is authoritatively Necromancer vs. Starcaller
        // ==================================================================

        [UnityTest]
        public IEnumerator Player_one_is_a_necromancer_without_any_manual_setup()
        {
            yield return LoadMatch();

            Assert.That(One().Hero.HeroPowerCardId.Value, Is.EqualTo("necromancer_choose_your_weapons"));
        }

        [UnityTest]
        public IEnumerator Player_two_is_a_starcaller_without_any_manual_setup()
        {
            yield return LoadMatch();

            Hero hero = Two().Hero;

            Assert.That(hero.HasHeroPower, Is.True,
                "Player two started the development match with no hero power.");
            Assert.That(hero.HeroPowerCardId.Value, Is.EqualTo("starcaller_lunar_phase"));
        }

        /// <summary>
        /// Not a visual fact: resolved the only way the engine ever resolves
        /// a class, by looking the hero power card up in the real catalog -
        /// the exact path <c>CardVisualSelection</c>/<c>CardVisualFactory</c>
        /// also use, and the one place a "presentation-only Starcaller"
        /// could not hide.
        /// </summary>
        [UnityTest]
        public IEnumerator Player_twos_class_resolves_as_starcaller_from_authoritative_state()
        {
            yield return LoadMatch();

            Assert.That(Session.State.Catalog.TryGet(Two().Hero.HeroPowerCardId, out CardDefinition definition),
                Is.True);
            Assert.That(definition.Class, Is.EqualTo(CardClass.Starcaller));
        }

        [UnityTest]
        public IEnumerator Player_ones_class_still_resolves_as_necromancer()
        {
            yield return LoadMatch();

            Assert.That(Session.State.Catalog.TryGet(One().Hero.HeroPowerCardId, out CardDefinition definition),
                Is.True);
            Assert.That(definition.Class, Is.EqualTo(CardClass.Necromancer));
        }

        // ==================================================================
        //  The medallion, once Player 2 is near
        // ==================================================================

        [UnityTest]
        public IEnumerator The_medallion_shows_lunar_phase_when_player_two_is_near()
        {
            yield return LoadMatch();
            yield return ReachPlayerTwosTurn();

            HeroPower.Refresh(Session, true);

            Assert.That(HeroPower.PlayerId, Is.EqualTo(PlayerId.Two));

            IPointerEnterHandler enterHandler = HeroPower;
            enterHandler.OnPointerEnter(new PointerEventData(EventSystem.current));

            Assert.That(HeroPower.TooltipTitle, Is.EqualTo("Lunar Phase"));
            Assert.That(HeroPower.TooltipBody, Does.Contain("2 Mana"),
                "Lunar Phase's mana cost is not showing as 2.");
        }

        [UnityTest]
        public IEnumerator The_medallion_binds_lunar_phases_own_center_art()
        {
            yield return LoadMatch();
            yield return ReachPlayerTwosTurn();

            HeroPower.Refresh(Session, true);

            Assert.That(HeroPower.CenterArtSprite, Is.Not.Null);
            Assert.That(HeroPower.CenterArtSprite.name, Is.EqualTo("LunarPhase_CenterArt"),
                "Starcaller's medallion is not drawing its own bound artwork.");

            // The frame is the same generic Hero Power ring Raise draws -
            // nothing Starcaller-specific was authored for it.
            Assert.That(HeroPower.FrameSprite, Is.Not.Null);
            Assert.That(HeroPower.FrameSprite, Is.Not.SameAs(HeroPower.CenterArtSprite));
        }

        // ==================================================================
        //  Interaction: immediate, no choice modal
        // ==================================================================

        [UnityTest]
        public IEnumerator Clicking_lunar_phase_activates_it_immediately_with_no_choice_screen()
        {
            yield return LoadMatch();
            yield return ReachPlayerTwosTurnWithMana(2);

            HeroPower.Refresh(Session, true);

            Button button = HeroPower.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, "Lunar Phase should be usable with enough mana.");

            int manaBefore = Two().AvailableMana;

            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerClickHandler);

            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(HeroPower.IsChoosing, Is.False,
                "Lunar Phase must never open Raise's choice modal - it has nothing to choose between.");

            Assert.That(Two().HasUsedHeroPowerThisTurn, Is.True);
            Assert.That(Two().AvailableMana, Is.EqualTo(manaBefore - 2));
            Assert.That(Two().SpellDamageBonus, Is.EqualTo(1),
                "The real click, through the real button, did not grant Spell Damage.");
        }

        [UnityTest]
        public IEnumerator Lunar_phase_becomes_disabled_for_the_rest_of_the_turn_after_use()
        {
            yield return LoadMatch();
            yield return ReachPlayerTwosTurnWithMana(2);

            HeroPower.Refresh(Session, true);
            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0)), Is.True);
            yield return null;
            yield return WaitUntilQueueIsIdle();

            HeroPower.Refresh(Session, true);

            Button button = HeroPower.GetComponent<Button>();
            Assert.That(button.interactable, Is.False,
                "Lunar Phase must be disabled after use, exactly like Raise.");
        }

        [UnityTest]
        public IEnumerator Lunar_phase_is_usable_again_on_player_twos_next_turn()
        {
            yield return LoadMatch();
            yield return ReachPlayerTwosTurnWithMana(2);

            HeroPower.Refresh(Session, true);
            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.Two, 0)), Is.True);
            yield return null;
            yield return WaitUntilQueueIsIdle();

            // Back around to player two: end their turn, then player one's.
            Session.Submit(new EndTurnCommand(PlayerId.Two));
            yield return null;
            yield return HandAtRest();

            yield return ReachPlayerTwosTurn();

            Assert.That(Two().SpellDamageBonus, Is.Zero,
                "Spell Damage must not still be active before Lunar Phase is used again.");

            HeroPower.Refresh(Session, true);

            Button button = HeroPower.GetComponent<Button>();
            Assert.That(button.interactable, Is.True,
                "Lunar Phase did not become usable again on player two's next turn.");
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
    }
}
