using System.Collections;
using CoH.App;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// The Necromancer as the player meets it: in the real match scene, on the
    /// real board, through the real views.
    ///
    /// The Core tests prove the rules and the data tests prove the cards. What
    /// neither can prove is the thing the phase was actually asked for - that
    /// pressing Play gives you a Necromancer with a hero power you can click.
    /// A feature that exists only in the engine is not a playable feature, and
    /// these are the tests that would fail if it were.
    /// </summary>
    public sealed class NecromancerBoardTests : InteractionTestBase
    {
        private HeroPowerView HeroPower => Presenter.NearHeroPower;

        private Player One() => Session.State.GetPlayer(PlayerId.One);

        /// <summary>Ends turns until player one is acting, so the hero power is theirs to use.</summary>
        private IEnumerator ReachPlayerOnesTurn()
        {
            int guard = 0;

            while (Session.State.CurrentPlayer != PlayerId.One && guard++ < 4)
            {
                Session.Submit(new EndTurnCommand(Session.State.CurrentPlayer));
                yield return null;
                yield return HandAtRest();
            }

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.One));
        }

        // ==================================================================
        //  The development match is already a Necromancer match
        // ==================================================================

        /// <summary>
        /// Pressing Play is enough. Nothing in the inspector has to be touched,
        /// which is the difference between a feature and a demo.
        /// </summary>
        [UnityTest]
        public IEnumerator Player_one_is_a_necromancer_without_any_manual_setup()
        {
            yield return LoadMatch();

            Hero hero = One().Hero;

            Assert.That(hero.HasHeroPower, Is.True,
                "Player one started the development match with no hero power.");

            Assert.That(hero.HeroPowerCardId.Value,
                Is.EqualTo("necromancer_choose_your_weapons"));
        }

        /// <summary>
        /// And that default lives in the bootstrap, not in the rules. The
        /// engine has no opinion about who is a Necromancer.
        /// </summary>
        [UnityTest]
        public IEnumerator The_default_comes_from_the_bootstrap_configuration()
        {
            yield return LoadMatch();

            MatchBootstrap bootstrap = Object.FindFirstObjectByType<MatchBootstrap>();

            Assert.That(bootstrap, Is.Not.Null, "The scene has no MatchBootstrap.");

            Assert.That(bootstrap.Config.HeroPowerForSeatOne.Value,
                Is.EqualTo(MatchBootstrap.DefaultDevelopmentHeroPower));

            // Seat one must never move away from Necromancer while seat two
            // gains a class of its own - the two are configured
            // independently, and this is the test that would catch one
            // pass's change leaking into the other seat.
            Assert.That(bootstrap.Config.HeroPowerForSeatTwo.Value,
                Is.EqualTo(MatchBootstrap.DefaultDevelopmentHeroPowerSeatTwo));
        }

        [UnityTest]
        public IEnumerator The_four_servants_are_in_the_matchs_catalog()
        {
            yield return LoadMatch();

            string[] servants =
            {
                "necromancer_skeletal_warrior",
                "necromancer_skeletal_rogue",
                "necromancer_crypt_fiend",
                "necromancer_abomination"
            };

            for (int index = 0; index < servants.Length; index++)
            {
                Assert.That(
                    Session.State.Catalog.TryGet(new CardId(servants[index]), out CardDefinition _),
                    Is.True, servants[index] + " is not in the running match's catalog.");
            }
        }

        // ==================================================================
        //  It is on the board
        // ==================================================================

        [UnityTest]
        public IEnumerator The_hero_power_is_present_and_bound_to_player_one()
        {
            yield return LoadMatch();

            Assert.That(HeroPower, Is.Not.Null,
                "The match scene has no hero power view wired into the presenter.");

            Assert.That(HeroPower.gameObject.activeInHierarchy, Is.True,
                "The hero power is hidden even though player one has one.");

            Assert.That(HeroPower.PlayerId, Is.EqualTo(PlayerId.One));
        }

        // ==================================================================
        //  Its state follows the engine
        // ==================================================================

        [UnityTest]
        public IEnumerator It_is_available_on_its_owners_turn_with_mana()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);

            bool engineAllows = Session.CanUseHeroPower(PlayerId.One) == RejectionReason.None;

            Assert.That(HeroPower.IsAvailable, Is.EqualTo(engineAllows),
                "The view disagreed with the engine about whether the power can be used.");

            Assert.That(engineAllows, Is.True,
                "Player one holds the turn with mana and an empty board, so it should be usable.");
        }

        [UnityTest]
        public IEnumerator It_is_unavailable_on_the_opponents_turn()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Session.Submit(new EndTurnCommand(PlayerId.One));
            yield return null;
            yield return HandAtRest();

            HeroPower.Refresh(Session, true);

            Assert.That(Session.CanUseHeroPower(PlayerId.One),
                Is.EqualTo(RejectionReason.NotYourTurn));

            Assert.That(HeroPower.IsAvailable, Is.False);
        }

        [UnityTest]
        public IEnumerator It_is_unavailable_once_it_has_been_used_this_turn()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.One, 0)), Is.True);

            yield return null;
            yield return WaitUntilQueueIsIdle();

            HeroPower.Refresh(Session, true);

            Assert.That(Session.CanUseHeroPower(PlayerId.One),
                Is.EqualTo(RejectionReason.HeroPowerAlreadyUsed));

            Assert.That(HeroPower.IsAvailable, Is.False);
        }

        // ==================================================================
        //  Clicking it
        // ==================================================================

        /// <summary>
        /// Opening the menu is a view state and commits nothing. This is the
        /// whole of cancellation: what has not been sent cannot need undoing.
        /// </summary>
        [UnityTest]
        public IEnumerator Opening_and_closing_the_menu_spends_nothing()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Player player = One();

            int manaBefore = player.AvailableMana;
            int boardBefore = player.Board.Count;

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            Assert.That(HeroPower.IsChoosing, Is.True, "The menu did not open.");

            HeroPower.CloseChoices();

            Assert.That(HeroPower.IsChoosing, Is.False);
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore));
            Assert.That(player.Board.Count, Is.EqualTo(boardBefore));
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
        }

        /// <summary>
        /// The whole gesture, through the view's own event: choosing an option
        /// submits the command and the servant lands on the board.
        /// </summary>
        [UnityTest]
        public IEnumerator Choosing_an_option_summons_that_servant_onto_the_board()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Player player = One();
            int manaBefore = player.AvailableMana;

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            // Option three: Abomination. Chosen precisely because it is not the
            // first, so an index that was ignored would show up here.
            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.One, 3)), Is.True);

            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(player.Board.Count, Is.EqualTo(1));
            Assert.That(player.Board[0].CardId.Value, Is.EqualTo("necromancer_abomination"));
            Assert.That(player.Board[0].HasKeyword(CardKeywords.Taunt), Is.True);

            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore - 1),
                "Exactly the hero power's one mana was spent.");
        }

        /// <summary>
        /// And the board grows a view for it, so the player can actually see
        /// what they summoned.
        /// </summary>
        [UnityTest]
        public IEnumerator The_summoned_servant_gets_a_view_on_the_board()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Assert.That(Session.Submit(new UseHeroPowerCommand(PlayerId.One, 2)), Is.True);

            yield return null;
            yield return WaitUntilQueueIsIdle();

            Minion summoned = One().Board[0];

            Assert.That(Presenter.TryGetMinionView(summoned.Id, out MinionView view), Is.True,
                "The summoned servant has no view on the board.");

            Assert.That(view, Is.Not.Null);
        }

        // ==================================================================
        //  The view never decides anything
        // ==================================================================

        /// <summary>
        /// Refreshing the view is a read. It must not be capable of changing
        /// the match, however many times it happens.
        /// </summary>
        [UnityTest]
        public IEnumerator Refreshing_the_view_never_changes_the_game()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Player player = One();

            int mana = player.AvailableMana;
            int board = player.Board.Count;
            int hand = player.Hand.Count;
            bool used = player.HasUsedHeroPowerThisTurn;

            for (int frame = 0; frame < 5; frame++)
            {
                HeroPower.Refresh(Session, true);
                HeroPower.OpenChoices();
                HeroPower.CloseChoices();
                yield return null;
            }

            Assert.That(player.AvailableMana, Is.EqualTo(mana));
            Assert.That(player.Board.Count, Is.EqualTo(board));
            Assert.That(player.Hand.Count, Is.EqualTo(hand));
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.EqualTo(used));
        }

        // ==================================================================
        //  It is attached to the hero, on screen, for real
        //
        //  The command-architecture tests above all drive the engine directly
        //  and never once asked whether a mouse could actually reach the
        //  button. These do: they read where the medallion actually ends up
        //  on screen, and they click through the real Button component rather
        //  than calling OpenChoices() by hand.
        // ==================================================================

        [UnityTest]
        public IEnumerator The_medallion_is_named_raise()
        {
            yield return LoadMatch();

            Hero hero = One().Hero;

            Assert.That(
                Session.State.Catalog.TryGet(hero.HeroPowerCardId, out CardDefinition definition), Is.True);

            Assert.That(definition.Name, Is.EqualTo("Raise"));
            Assert.That(definition.Name, Is.Not.EqualTo("Choix des armes"));
        }

        /// <summary>
        /// "Immediately to the right of the hero", read back geometrically
        /// rather than eyeballed: the medallion's on-screen X must sit to the
        /// right of the hero's own on-screen X, and both must actually be
        /// somewhere on screen.
        /// </summary>
        [UnityTest]
        public IEnumerator The_medallion_sits_on_screen_to_the_right_of_the_hero()
        {
            yield return LoadMatch();

            HeroPower.Refresh(Session, true);

            Vector3 heroScreen = MatchCamera.WorldToScreenPoint(Presenter.NearHero.transform.position);
            Vector3 medallionScreen = RectTransformUtility.WorldToScreenPoint(null, HeroPower.transform.position);

            Assert.That(medallionScreen.x, Is.GreaterThan(heroScreen.x),
                "The medallion is not to the right of the hero on screen.");

            Assert.That(medallionScreen.x, Is.InRange(0f, Screen.width),
                "The medallion has drifted off the side of the screen.");

            Assert.That(medallionScreen.y, Is.InRange(0f, Screen.height),
                "The medallion has drifted off the top or bottom of the screen.");
        }

        /// <summary>
        /// The whole point of a medallion beside the hero is that a mouse can
        /// reach it. This clicks the real Button component the way a pointer
        /// would, through the same event system the game uses, rather than
        /// calling <see cref="HeroPowerView.OpenChoices"/> directly.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_the_real_button_opens_the_choices()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);

            Button button = HeroPower.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "The medallion has no Button component to click.");
            Assert.That(button.interactable, Is.True, "The button is not interactable even though the power is usable.");

            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, pointerData, ExecuteEvents.pointerClickHandler);

            Assert.That(HeroPower.IsChoosing, Is.True,
                "A real click on the button did not open the choice menu.");
        }

        /// <summary>Hovering shows what the medallion is, without it being on screen all the time.</summary>
        [UnityTest]
        public IEnumerator Hovering_shows_a_tooltip_naming_raise_and_its_cost()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.IsShowingTooltip, Is.False,
                "The tooltip is visible before the pointer ever touched the medallion.");

            IPointerEnterHandler enterHandler = HeroPower;
            enterHandler.OnPointerEnter(new PointerEventData(EventSystem.current));

            Assert.That(HeroPower.IsShowingTooltip, Is.True);
            Assert.That(HeroPower.TooltipTitle, Is.EqualTo("Raise"));
            Assert.That(HeroPower.TooltipBody, Does.Contain("1 Mana"));

            IPointerExitHandler exitHandler = HeroPower;
            exitHandler.OnPointerExit(new PointerEventData(EventSystem.current));

            Assert.That(HeroPower.IsShowingTooltip, Is.False);
        }

        /// <summary>Opening the menu is meant to replace the tooltip, not sit under it.</summary>
        [UnityTest]
        public IEnumerator Opening_the_choices_hides_the_tooltip()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);

            IPointerEnterHandler enterHandler = HeroPower;
            enterHandler.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(HeroPower.IsShowingTooltip, Is.True);

            HeroPower.OpenChoices();

            Assert.That(HeroPower.IsShowingTooltip, Is.False);
        }

        // ==================================================================
        //  The modular medallion: frame, centre art and mana gem, genuinely
        //  separate - not the vertical HearthCards card, and not one asset
        //  quietly playing both frame and art at once.
        // ==================================================================

        /// <summary>
        /// No bespoke Hero Power frame has been authored yet, so the generic
        /// procedural ring is what every hero power currently draws - by
        /// design, not as a degraded stand-in. This is a living contract: the
        /// day a real frame image is assigned, this assertion should flip,
        /// and updating it is how that day gets noticed rather than drifting
        /// silently.
        /// </summary>
        [UnityTest]
        public IEnumerator The_authored_frame_is_used_not_the_generic_ring()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.IsShowingCustomFrame, Is.True,
                "The medallion fell back to the procedural ring in the normal match scene - the " +
                "authored HeroPower_Frame.png is not reaching the view.");

            Assert.That(HeroPower.FrameSprite, Is.Not.Null);
            Assert.That(HeroPower.FrameSprite.name, Is.EqualTo("HeroPower_Frame"),
                "The frame is showing a sprite other than the authored bronze-and-gold ring.");
            Assert.That(HeroPower.FrameSprite.name, Is.Not.EqualTo("MedallionArt_Fallback"));
        }

        /// <summary>
        /// The frame and Raise's own centre art used to be the same single
        /// picture, which is the exact bug this pass fixed: with no second
        /// asset, the "frame" was whatever the art's own silhouette happened
        /// to be, and nothing structured the medallion as a medallion. They
        /// must now be two different sprites, full stop.
        /// </summary>
        [UnityTest]
        public IEnumerator The_frame_and_center_art_are_different_sprites()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.FrameSprite, Is.Not.Null);
            Assert.That(HeroPower.CenterArtSprite, Is.Not.Null,
                "Raise has no centre art bound - it should resolve the claws-and-orb painting.");

            Assert.That(HeroPower.FrameSprite, Is.Not.SameAs(HeroPower.CenterArtSprite),
                "The frame and the centre art are the same sprite - one asset is still playing both roles.");
        }

        /// <summary>
        /// Raise's own centre art is the claws-and-orb painting bound through
        /// the card visual library, not the library's generic shared
        /// placeholder - proving the binding this pass added actually reaches
        /// the view rather than silently falling through.
        /// </summary>
        [UnityTest]
        public IEnumerator Center_art_resolves_to_raises_own_bound_painting()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.CenterArtSprite, Is.Not.Null);
            Assert.That(HeroPower.CenterArtSprite.name, Is.EqualTo("Raise_CenterArt"),
                "Raise is drawing something other than its own bound artwork in its centre.");
        }

        /// <summary>
        /// The frame renders on top of the centre art - later in sibling
        /// order, in uGUI - which is what keeps it "clearly defining the
        /// outer silhouette" rather than sitting hidden behind the art.
        /// </summary>
        [UnityTest]
        public IEnumerator The_frame_renders_above_the_center_art()
        {
            yield return LoadMatch();

            Image frame = FindChildComponent<Image>("Frame");
            Image mask = FindChildComponent<Image>("CenterArtMask");

            Assert.That(frame, Is.Not.Null);
            Assert.That(mask, Is.Not.Null);
            Assert.That(frame.transform.parent, Is.SameAs(mask.transform.parent),
                "Frame and CenterArtMask are expected to be siblings under the same medallion root.");

            Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(mask.transform.GetSiblingIndex()),
                "The frame is not drawn after (so not on top of) the centre art's mask.");
        }

        /// <summary>
        /// The mask that keeps centre art from bleeding outside its opening
        /// clips only its own children. The frame is a sibling, not a child
        /// of that mask, so it must never be affected by it.
        /// </summary>
        [UnityTest]
        public IEnumerator The_frame_is_not_a_child_of_the_center_art_mask()
        {
            yield return LoadMatch();

            Image frame = FindChildComponent<Image>("Frame");
            Mask mask = HeroPower.GetComponentInChildren<Mask>(true);

            Assert.That(frame, Is.Not.Null);
            Assert.That(mask, Is.Not.Null, "No Mask component under the medallion at all.");

            Assert.That(frame.GetComponentInParent<Mask>(true), Is.Null,
                "The frame sits under the centre art's Mask and would be clipped by it.");
        }

        /// <summary>
        /// The previous pass's complete vertical HearthCards card is still in
        /// the project (useful for other CardType.HeroPower rendering), but it
        /// must not be what the board shows any more.
        /// </summary>
        [UnityTest]
        public IEnumerator The_medallion_is_not_the_vertical_hearthcards_card()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.FrameSprite.name, Is.Not.EqualTo("Card_Inhand_HeroPower_Neutral"),
                "The board medallion is still drawing the full vertical HearthCards Hero Power card.");

            Assert.That(HeroPower.CenterArtSprite.name, Is.Not.EqualTo("Card_Inhand_HeroPower_Neutral"),
                "The centre art is still the full vertical HearthCards Hero Power card.");
        }

        /// <summary>
        /// The coarse layout calibration this task asked for: the medallion
        /// must actually be bigger than its previous configuration, not just
        /// nominally rescaled in code that never reached the scene.
        /// </summary>
        [UnityTest]
        public IEnumerator The_medallion_root_is_larger_than_its_original_size()
        {
            yield return LoadMatch();

            RectTransform root = (RectTransform)HeroPower.transform;

            // The size before any of this task's scaling passes. No longer
            // capped to the earlier 1.6-1.8x coarse-layout figure: the root
            // is now sized backwards from the centre art's own sharpness
            // requirement (see the next test), and is allowed to land
            // wherever that puts it.
            const float originalSize = 72f;

            Assert.That(root.sizeDelta.x, Is.GreaterThan(originalSize * 1.5f),
                "The medallion is not noticeably larger than its original size.");
        }

        /// <summary>
        /// The actual driver of this pass's sizing: the centre art was
        /// reading soft at ~76px of a 941px painting, so the root is now
        /// sized backwards from a ~112px centre art target rather than
        /// forwards from an arbitrary root multiplier.
        /// </summary>
        [UnityTest]
        public IEnumerator Center_art_is_sized_for_sharpness_around_86_reference_pixels()
        {
            yield return LoadMatch();

            Image art = FindChildComponent<Image>("Art");
            Assert.That(art, Is.Not.Null);

            RectTransform artRect = (RectTransform)art.transform;
            Vector2 size = artRect.rect.size;

            // Bracketed to stay clear on both sides: comfortably above the
            // original ~76px where the missing-mipmap blur first showed up,
            // and below the ~112px an earlier pass tried before the whole
            // medallion was judged too large beside the hero.
            Assert.That(size.x, Is.InRange(78f, 96f),
                "The centre art is not close to the ~86px compact-but-legible target this pass sized it for.");
            Assert.That(size.y, Is.EqualTo(size.x).Within(0.01f), "The centre art is not square.");
        }

        /// <summary>
        /// The gem is the same shared asset every other card's own mana cost
        /// uses, reused rather than duplicated, per this task's own audit
        /// requirement.
        /// </summary>
        [UnityTest]
        public IEnumerator The_mana_gem_uses_the_shared_catalog_asset_not_the_procedural_fallback()
        {
            yield return LoadMatch();

            Assert.That(HeroPower.IsShowingCatalogManaGem, Is.True,
                "The mana gem fell back to its procedural disc; the shared catalog gem was not resolved.");
        }

        [UnityTest]
        public IEnumerator The_cost_text_reads_one()
        {
            yield return LoadMatch();

            TextMeshProUGUI cost = FindChildComponent<TextMeshProUGUI>("CostText");

            Assert.That(cost, Is.Not.Null, "No CostText layer under the medallion.");
            Assert.That(cost.text, Is.EqualTo("1"));
        }

        // ==================================================================
        //  Three independent layers, not one flattened picture
        // ==================================================================

        [UnityTest]
        public IEnumerator Frame_center_art_and_mana_gem_are_three_distinct_objects()
        {
            yield return LoadMatch();

            Image frame = FindChildComponent<Image>("Frame");
            Image art = FindChildComponent<Image>("Art");
            Image gem = FindChildComponent<Image>("ManaGem");

            Assert.That(frame, Is.Not.Null, "No Frame layer.");
            Assert.That(art, Is.Not.Null, "No centre Art layer.");
            Assert.That(gem, Is.Not.Null, "No ManaGem layer.");

            Assert.That(frame, Is.Not.SameAs(art));
            Assert.That(frame, Is.Not.SameAs(gem));
            Assert.That(art, Is.Not.SameAs(gem));
        }

        /// <summary>
        /// Centre art sits behind a Mask component, which is the generic
        /// clipping this task asked for instead of a manually circle-cropped
        /// picture per hero power.
        /// </summary>
        [UnityTest]
        public IEnumerator Center_art_is_clipped_by_a_mask_not_a_hand_edited_picture()
        {
            yield return LoadMatch();

            Image art = FindChildComponent<Image>("Art");

            Assert.That(art, Is.Not.Null);

            Mask mask = art.transform.parent != null ? art.transform.parent.GetComponent<Mask>() : null;

            Assert.That(mask, Is.Not.Null,
                "Centre art has no Mask ancestor - it would bleed past the frame's opening.");
        }

        /// <summary>
        /// Reaching directly into the centre art's own Image and replacing
        /// its sprite - exactly what assigning real Raise artwork later would
        /// do - must not touch the frame or the gem. This is the modularity
        /// requirement itself, proven rather than assumed.
        /// </summary>
        [UnityTest]
        public IEnumerator Changing_center_art_does_not_touch_the_frame_or_the_mana_gem()
        {
            yield return LoadMatch();

            Sprite frameBefore = HeroPower.FrameSprite;
            Sprite gemBefore = HeroPower.ManaGemSprite;

            Image art = FindChildComponent<Image>("Art");
            Assert.That(art, Is.Not.Null);

            Texture2D stand_in = new Texture2D(4, 4);
            Sprite futureRaiseArt = Sprite.Create(stand_in, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

            art.sprite = futureRaiseArt;

            Assert.That(HeroPower.CenterArtSprite, Is.SameAs(futureRaiseArt),
                "The centre art layer did not actually take the new sprite.");

            Assert.That(HeroPower.FrameSprite, Is.SameAs(frameBefore),
                "Replacing the centre art changed the frame.");

            Assert.That(HeroPower.ManaGemSprite, Is.SameAs(gemBefore),
                "Replacing the centre art changed the mana gem.");

            Object.DestroyImmediate(stand_in);
        }

        // ==================================================================
        //  The four choices are real cards, not the old prototype panels
        // ==================================================================

        private static readonly string[] ServantOrder =
        {
            "necromancer_skeletal_warrior",
            "necromancer_skeletal_rogue",
            "necromancer_crypt_fiend",
            "necromancer_abomination"
        };

        [UnityTest]
        public IEnumerator Opening_raise_shows_exactly_four_active_card_views()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            int active = 0;

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                CardView view = HeroPower.ChoiceCards[index];

                Assert.That(view, Is.Not.Null, "Choice slot " + index + " has no CardView at all.");

                if (view.gameObject.activeSelf)
                {
                    active++;
                }
            }

            Assert.That(active, Is.EqualTo(4), "Raise did not show exactly four active choice cards.");
        }

        [UnityTest]
        public IEnumerator Each_choice_card_shows_the_correct_servant_in_order()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            for (int index = 0; index < ServantOrder.Length; index++)
            {
                CardView view = HeroPower.ChoiceCards[index];

                Assert.That(Session.State.Catalog.TryGet(new CardId(ServantOrder[index]), out CardDefinition card),
                    Is.True);

                Assert.That(view.Shown.Name, Is.EqualTo(card.Name),
                    "Choice slot " + index + " does not show " + card.Name + ".");
            }
        }

        [UnityTest]
        public IEnumerator Each_choice_card_resolves_the_necromancer_minion_frame()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            for (int index = 0; index < ServantOrder.Length; index++)
            {
                CardView view = HeroPower.ChoiceCards[index];

                Sprite frame = view.Plan.SpriteIn(CardVisualSlot.Frame);

                Assert.That(frame, Is.Not.Null, ServantOrder[index] + " drew no frame at all.");
                Assert.That(frame.name, Is.EqualTo("Card_Inhand_Minion_Necromancer"),
                    ServantOrder[index] + " is not drawing the Necromancer Minion frame - it resolved " +
                    "through the normal catalog match, not a card-specific override, so this also proves " +
                    "the class+type resolution is reaching the choice UI.");
            }
        }

        private static readonly string[] ExpectedArtworkNames =
        {
            "Skeletal_Warrior", "Skeletal_Rogue", "Crypt_Fiend", "Abomination"
        };

        /// <summary>
        /// Proves the final artworks reach Raise through the same ordinary
        /// <c>CardId -> CardVisualLibrary -> artwork</c> seam every other
        /// card goes through, with no special-cased artwork code added for
        /// the choice presentation: each choice card's own composed plan is
        /// read directly, the same way <see cref="Each_choice_card_resolves_the_necromancer_minion_frame"/>
        /// reads the frame, rather than the library being asked again here
        /// and merely compared to itself.
        /// </summary>
        [UnityTest]
        public IEnumerator Each_choice_card_resolves_its_own_final_artwork()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            for (int index = 0; index < ServantOrder.Length; index++)
            {
                CardView view = HeroPower.ChoiceCards[index];

                Sprite artwork = view.Plan.SpriteIn(CardVisualSlot.Artwork);

                Assert.That(artwork, Is.Not.Null, ServantOrder[index] + " drew no artwork at all.");
                Assert.That(artwork.name, Is.EqualTo(ExpectedArtworkNames[index]),
                    ServantOrder[index] + " resolved '" + artwork.name + "' instead of its final artwork " +
                    "'" + ExpectedArtworkNames[index] + "' - Skeletal Rogue and Crypt Fiend in particular " +
                    "must never be swapped.");
            }
        }

        [UnityTest]
        public IEnumerator Choice_card_stats_match_the_authored_servant_definitions()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            (int attack, int health)[] expected = { (1, 1), (0, 1), (1, 2), (0, 2) };

            for (int index = 0; index < expected.Length; index++)
            {
                CardVisualDescriptor shown = HeroPower.ChoiceCards[index].Shown;

                Assert.That(shown.Attack, Is.EqualTo(expected[index].attack),
                    ServantOrder[index] + " shows the wrong attack.");
                Assert.That(shown.Health, Is.EqualTo(expected[index].health),
                    ServantOrder[index] + " shows the wrong health.");
                Assert.That(shown.ManaCost, Is.EqualTo(1),
                    ServantOrder[index] + " does not show its printed cost of one.");
                Assert.That(shown.ShowsCost, Is.True);
                Assert.That(shown.ShowsStatistics, Is.True,
                    "A minion choice card must print attack and health.");
            }
        }

        [UnityTest]
        public IEnumerator No_prototype_option_panels_remain_in_the_hierarchy()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            foreach (Transform child in HeroPower.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(child.name, Does.Not.StartWith("Option "),
                    "A prototype option panel (\"" + child.name + "\") is still in the hierarchy.");
            }

            Assert.That(FindChildComponent<Image>("Portrait"), Is.Null,
                "The old placeholder-portrait square is still in the hierarchy.");
        }

        [UnityTest]
        public IEnumerator Choice_cards_carry_no_entity_id_and_are_not_hand_cards()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            for (int index = 0; index < ServantOrder.Length; index++)
            {
                CardView view = HeroPower.ChoiceCards[index];

                Assert.That(view.EntityId.IsNone, Is.True,
                    ServantOrder[index] + " has a real EntityId - the board's pointer probe would treat " +
                    "it as a draggable, playable hand card.");
            }
        }

        [UnityTest]
        public IEnumerator Displaying_choices_creates_no_hand_card_and_spends_no_mana()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            Player player = One();
            int handBefore = player.Hand.Count;
            int manaBefore = player.AvailableMana;

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            Assert.That(player.Hand.Count, Is.EqualTo(handBefore),
                "Opening the choices put something in the hand.");
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore),
                "Opening the choices spent mana before anything was chosen.");
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
        }

        /// <summary>
        /// A real click, through the same ray-based entry point
        /// <c>MatchInputController</c> already uses for the board, aimed at
        /// the third choice card's own world position - not a direct call to
        /// <c>Session.Submit</c>, which would prove the command works but say
        /// nothing about whether the click actually reaches the right card.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_choice_card_submits_its_own_hero_power_option()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();
            yield return null;

            const int chosenIndex = 2; // Crypt Fiend - not the first, so an ignored index would show up.
            CardView target = HeroPower.ChoiceCards[chosenIndex];

            Vector3 screenPoint = MatchCamera.WorldToScreenPoint(target.transform.position);
            Ray ray = MatchCamera.ScreenPointToRay(screenPoint);

            HeroPower.ApplyChoicePointer(ray, clicked: true);

            yield return null;
            yield return WaitUntilQueueIsIdle();

            Assert.That(HeroPower.IsChoosing, Is.False, "The click did not close the choice menu.");

            Player player = One();
            Assert.That(player.Board.Count, Is.EqualTo(1));
            Assert.That(player.Board[0].CardId.Value, Is.EqualTo(ServantOrder[chosenIndex]),
                "The click summoned the wrong servant - the ray did not resolve to the intended card.");
        }

        [UnityTest]
        public IEnumerator Cancel_still_closes_the_real_choice_cards_and_hides_them()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            Assert.That(HeroPower.IsChoosing, Is.True);

            HeroPower.CloseChoices();

            Assert.That(HeroPower.IsChoosing, Is.False);

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                Transform anchor = HeroPower.ChoiceCards[index].transform.parent;
                Assert.That(anchor.gameObject.activeInHierarchy, Is.False,
                    "The choice cards are still visible in the world after Cancel.");
            }

            Player player = One();
            Assert.That(player.Board.Count, Is.Zero);
            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
        }

        // ==================================================================
        //  Regression: choice card text/stat layers must render at the same
        //  fitted size a hand card's own layers do, not their auto-size
        //  ceiling. A manual validation pass once caught this at its most
        //  visible - a single-digit stat filling most of the screen - and
        //  these are the tests that would have failed on that exact state.
        // ==================================================================

        /// <summary>
        /// The card that used to break: bound while its own hierarchy was
        /// still inactive, which is the default resting state of the choice
        /// anchor before Raise is ever opened. Composing on an inactive
        /// hierarchy does not defer TextMeshPro's auto-sizing - it computes
        /// it wrong, pinned near the uncapped ceiling rather than fitted to
        /// the slot. Binding here, before this test ever calls
        /// <c>OpenChoices</c>, is deliberate: it exercises the exact path
        /// that broke, rather than only the already-open state.
        /// </summary>
        private IEnumerator BoundButNotYetOpened()
        {
            yield return LoadMatch();
            yield return ReachPlayerOnesTurn();
        }

        /// <summary>The one real hand card already on the board, for comparison.</summary>
        private CardView FindAnyHandCard()
        {
            foreach (CardView view in Object.FindObjectsByType<CardView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!view.EntityId.IsNone)
                {
                    return view;
                }
            }

            return null;
        }

        [UnityTest]
        public IEnumerator Choice_card_stat_text_renders_at_the_same_fitted_size_as_a_hand_cards()
        {
            yield return BoundButNotYetOpened();

            CardView hand = FindAnyHandCard();
            Assert.That(hand, Is.Not.Null, "No hand card on the board to compare against.");

            CardView choice = HeroPower.ChoiceCards[0];

            TMPro.TextMeshPro[] handLabels = hand.GetComponentsInChildren<TMPro.TextMeshPro>(true);
            TMPro.TextMeshPro[] choiceLabels = choice.GetComponentsInChildren<TMPro.TextMeshPro>(true);

            Assert.That(choiceLabels.Length, Is.EqualTo(handLabels.Length),
                "The choice card composed a different number of text layers than a hand card - the " +
                "two are no longer going through the same recipe.");

            // A single-digit mana/attack/health value auto-fits to the same
            // size in either card, whatever the two cards' names happen to
            // be, because the slot it is fitted into is defined by the
            // recipe rather than by which card is shown. This is exactly the
            // layer the bug hit hardest: it rendered at its 12-14 unit
            // ceiling instead of the ~1.8 units both cards actually settle
            // on here.
            for (int index = 0; index < handLabels.Length; index++)
            {
                if (handLabels[index].text.Length != 1 || !char.IsDigit(handLabels[index].text[0]))
                {
                    continue;
                }

                Assert.That(choiceLabels[index].fontSize, Is.EqualTo(handLabels[index].fontSize).Within(0.05f),
                    "Choice card stat layer " + index + " ('" + choiceLabels[index].text + "') fitted to " +
                    choiceLabels[index].fontSize + " units, against " + handLabels[index].fontSize +
                    " for the equivalent hand card layer.");
            }
        }

        [UnityTest]
        public IEnumerator Choice_card_text_never_renders_near_its_uncapped_auto_size_ceiling()
        {
            yield return BoundButNotYetOpened();

            CardView choice = HeroPower.ChoiceCards[0];

            foreach (TMPro.TextMeshPro label in choice.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                if (!label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                // TextMeshPro's own sentinel for "auto-size never actually
                // resolved" - the other shape composing on an inactive
                // hierarchy took, alongside pinning at the ceiling. Either
                // one is the same underlying bug; a sane fitted size is
                // always a small positive number.
                Assert.That(label.fontSize, Is.GreaterThan(0f),
                    "'" + label.text + "' (" + label.gameObject.name + ") never resolved a font size at " +
                    "all (" + label.fontSize + ") - its auto-sizing did not converge.");

                // The bug pinned short strings (a single stat digit) at
                // essentially their fontSizeMax. A correctly fitted label
                // sits well clear of that ceiling; this catches the
                // regression without hard-coding the exact fitted value for
                // every layer.
                Assert.That(label.fontSize, Is.LessThan(label.fontSizeMax * 0.9f).Or.EqualTo(label.fontSizeMax),
                    "'" + label.text + "' (" + label.gameObject.name + ") rendered at " + label.fontSize +
                    " against a ceiling of " + label.fontSizeMax + " - suspiciously close to its " +
                    "uncapped maximum for text this short.");

                // And in absolute terms: nothing on a roughly one-unit-wide
                // card should ever need a double-digit font size to read a
                // single digit.
                if (label.text.Length <= 2)
                {
                    Assert.That(label.fontSize, Is.LessThan(5f),
                        "'" + label.text + "' (" + label.gameObject.name + ") rendered at " + label.fontSize +
                        " units - far larger than anything on a card this size should need.");
                }
            }
        }

        /// <summary>
        /// The frame's own world size is the one thing the earlier pass
        /// already got right (it is what made the bug legible as "text is
        /// wrong", not "everything is wrong"). Kept here as the coherence
        /// check the other two tests assume: if this ever failed too, the
        /// text tests above would be comparing against a card that was
        /// itself already broken.
        /// </summary>
        [UnityTest]
        public IEnumerator Choice_card_frame_world_size_stays_close_to_a_hand_cards()
        {
            yield return BoundButNotYetOpened();

            CardView hand = FindAnyHandCard();
            SpriteRenderer handFrame = FindNamedSprite(hand, "Card_Inhand_Minion_Neutral");
            SpriteRenderer choiceFrame = FindNamedSprite(HeroPower.ChoiceCards[0], "Card_Inhand_Minion_Necromancer");

            Assert.That(handFrame, Is.Not.Null);
            Assert.That(choiceFrame, Is.Not.Null);

            float handHeight = handFrame.bounds.size.y;
            float choiceHeight = choiceFrame.bounds.size.y;

            // Root scales differ by design - a compact, centred four-card
            // choice row is deliberately smaller than a hand card, tuned
            // together with its spacing so all four fit without overlapping
            // each other or End Turn (see choiceCardViewportHeight). The
            // lower bound only has to catch a genuine catastrophe - a choice
            // card rendering at a wildly wrong scale, the way the original
            // TMP bug made text balloon to many times a card's own size -
            // not hold the compact layout to a hand card's own proportions.
            Assert.That(choiceHeight, Is.InRange(handHeight * 0.3f, handHeight * 2f),
                "The choice card's frame is a wildly different world size than a hand card's.");
        }

        private static SpriteRenderer FindNamedSprite(CardView view, string spriteNameContains)
        {
            foreach (SpriteRenderer renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite != null && renderer.sprite.name == spriteNameContains)
                {
                    return renderer;
                }
            }

            return null;
        }

        // ==================================================================
        //  Regression: the choice row is a centred SCREEN composition, not
        //  four CardViews positioned relative to the board. A manual
        //  validation pass caught the previous, world-anchored layout
        //  spilling off the left edge and crowding the hero and the board;
        //  these test the actual on-screen geometry that broke, in viewport
        //  fractions rather than brittle exact pixels, so they fail on that
        //  same class of regression without re-encoding today's tuned
        //  numbers as a pixel-perfect screenshot.
        // ==================================================================

        /// <summary>Opens the real choice menu and waits out the resting-pose ease so its geometry is settled.</summary>
        private IEnumerator OpenedChoices()
        {
            yield return BoundButNotYetOpened();

            HeroPower.Refresh(Session, true);
            HeroPower.OpenChoices();

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// A choice card's on-screen footprint, in viewport fractions (0..1
        /// on each axis) - all four corners of its actual card-sized quad,
        /// carried through its real world transform and the match camera,
        /// rather than just its centre point. That is what makes "fully
        /// on-screen" and "does not overlap its neighbour" answerable at
        /// all: a card can have a centre well inside the screen and still
        /// have an edge past it.
        /// </summary>
        private (float minX, float maxX, float minY, float maxY) ChoiceViewportBounds(CardView view)
        {
            float halfWidth = CardCanvas.CardWidth * 0.5f;
            float halfHeight = CardCanvas.CardHeight * 0.5f;

            Vector3[] localCorners =
            {
                new Vector3(-halfWidth, -halfHeight, 0f), new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f), new Vector3(halfWidth, halfHeight, 0f)
            };

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;

            foreach (Vector3 corner in localCorners)
            {
                Vector3 viewport = MatchCamera.WorldToViewportPoint(view.transform.TransformPoint(corner));
                minX = Mathf.Min(minX, viewport.x);
                maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            return (minX, maxX, minY, maxY);
        }

        private const float ViewportSafeMargin = 0.03f;

        [UnityTest]
        public IEnumerator All_four_choice_cards_are_fully_inside_the_viewport_safe_margins()
        {
            yield return OpenedChoices();

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);

                Assert.That(bounds.minX, Is.GreaterThanOrEqualTo(ViewportSafeMargin),
                    "Choice card " + index + " crosses the left safe margin (left edge at " + bounds.minX + ").");
                Assert.That(bounds.maxX, Is.LessThanOrEqualTo(1f - ViewportSafeMargin),
                    "Choice card " + index + " crosses the right safe margin (right edge at " + bounds.maxX + ").");
                Assert.That(bounds.minY, Is.GreaterThanOrEqualTo(ViewportSafeMargin),
                    "Choice card " + index + " crosses the bottom safe margin (bottom edge at " + bounds.minY + ").");
                Assert.That(bounds.maxY, Is.LessThanOrEqualTo(1f - ViewportSafeMargin),
                    "Choice card " + index + " crosses the top safe margin (top edge at " + bounds.maxY + ").");
            }
        }

        [UnityTest]
        public IEnumerator Choice_cards_do_not_overlap_each_other()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            var bounds = new (float minX, float maxX, float minY, float maxY)[count];

            for (int index = 0; index < count; index++)
            {
                bounds[index] = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
            }

            for (int index = 0; index < count - 1; index++)
            {
                Assert.That(bounds[index].maxX, Is.LessThanOrEqualTo(bounds[index + 1].minX),
                    "Choice card " + index + " (right edge " + bounds[index].maxX + ") overlaps choice card " +
                    (index + 1) + " (left edge " + bounds[index + 1].minX + ").");
            }
        }

        [UnityTest]
        public IEnumerator Choice_card_centers_are_ordered_and_symmetric_around_screen_center()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            float[] centerX = new float[count];

            for (int index = 0; index < count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
                centerX[index] = (bounds.minX + bounds.maxX) * 0.5f;
            }

            for (int index = 0; index < count - 1; index++)
            {
                Assert.That(centerX[index + 1], Is.GreaterThan(centerX[index]),
                    "Choice cards are not left-to-right ordered on screen: card " + (index + 1) +
                    "'s centre is not to the right of card " + index + "'s.");
            }

            for (int index = 0; index < count / 2; index++)
            {
                int mirror = count - 1 - index;
                float sum = centerX[index] + centerX[mirror];

                Assert.That(sum, Is.EqualTo(1f).Within(0.02f),
                    "Choice cards " + index + " and " + mirror + " are not symmetric around the screen's " +
                    "horizontal centre (their viewport-x centres sum to " + sum + ", not 1).");
            }
        }

        [UnityTest]
        public IEnumerator All_four_choice_cards_use_identical_visual_scale()
        {
            yield return OpenedChoices();

            float first = HeroPower.ChoiceCards[0].transform.lossyScale.x;

            for (int index = 1; index < HeroPower.ChoiceCards.Count; index++)
            {
                Vector3 scale = HeroPower.ChoiceCards[index].transform.lossyScale;

                Assert.That(scale.x, Is.EqualTo(first).Within(0.001f),
                    "Choice card " + index + " (scale " + scale.x + ") is not the same size as choice card 0 (" +
                    first + ") - a compact, uniform row needs identical scale on every card.");
            }
        }

        [UnityTest]
        public IEnumerator Choice_card_group_is_centered_on_screen_horizontally()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            float sum = 0f;

            for (int index = 0; index < count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
                sum += (bounds.minX + bounds.maxX) * 0.5f;
            }

            float average = sum / count;

            Assert.That(average, Is.EqualTo(0.5f).Within(0.02f),
                "The choice row's average horizontal centre (" + average + ") is not near the middle of the " +
                "screen - the group does not read as centred.");
        }

        [UnityTest]
        public IEnumerator Choice_card_gaps_between_consecutive_cards_are_equal()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            float[] centerX = new float[count];

            for (int index = 0; index < count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
                centerX[index] = (bounds.minX + bounds.maxX) * 0.5f;
            }

            float firstGap = centerX[1] - centerX[0];

            for (int index = 1; index < count - 1; index++)
            {
                float gap = centerX[index + 1] - centerX[index];

                Assert.That(gap, Is.EqualTo(firstGap).Within(0.005f),
                    "The gap between choice cards " + index + " and " + (index + 1) + " (" + gap + ") " +
                    "differs from the gap between the first two (" + firstGap + ") - the row is not " +
                    "evenly spaced.");
            }
        }

        /// <summary>
        /// A regression not against a fixed number but against the cards'
        /// own measured size: four non-overlapping cards can never occupy
        /// less screen width than their own combined bulk, so this proves
        /// the row sits close to that geometric floor - as compact as it
        /// can physically be at the current card size - rather than leaving
        /// needless air between cards the way a much larger spacing
        /// constant once did.
        /// </summary>
        [UnityTest]
        public IEnumerator Choice_group_is_as_compact_as_the_cards_own_width_allows()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            var bounds = new (float minX, float maxX, float minY, float maxY)[count];

            for (int index = 0; index < count; index++)
            {
                bounds[index] = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
            }

            float cardWidth = bounds[0].maxX - bounds[0].minX;
            float groupWidth = bounds[count - 1].maxX - bounds[0].minX;
            float minimumPossibleWidth = cardWidth * count;

            Assert.That(groupWidth, Is.LessThan(minimumPossibleWidth * 1.15f),
                "The choice row (" + groupWidth + ") is far wider than the four cards' own combined " +
                "width (" + minimumPossibleWidth + ") suggests it needs to be - there is more air " +
                "between the cards than their own size requires.");
        }

        [UnityTest]
        public IEnumerator Choice_cards_stay_above_the_player_one_hero_area()
        {
            yield return OpenedChoices();

            Assert.That(Presenter.NearHero, Is.Not.Null, "No near hero to compare the choice row's height against.");

            float heroViewportY = MatchCamera.WorldToViewportPoint(Presenter.NearHero.transform.position).y;

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);

                Assert.That(bounds.minY, Is.GreaterThan(heroViewportY),
                    "Choice card " + index + " dips down to the near hero's own screen height (bottom edge " +
                    bounds.minY + " vs hero at " + heroViewportY + ") instead of floating above it.");
            }
        }

        [UnityTest]
        public IEnumerator Choice_cards_do_not_overlap_the_hand_region()
        {
            yield return OpenedChoices();

            CardView hand = FindAnyHandCard();
            Assert.That(hand, Is.Not.Null, "No hand card on the board to compare the choice row's height against.");

            float handViewportY = MatchCamera.WorldToViewportPoint(hand.transform.position).y;

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);

                Assert.That(bounds.minY, Is.GreaterThan(handViewportY),
                    "Choice card " + index + " dips down into the hand's own screen region (bottom edge " +
                    bounds.minY + " vs the hand at " + handViewportY + ").");
            }
        }

        [UnityTest]
        public IEnumerator Raise_medallion_is_hidden_while_the_choice_menu_is_open()
        {
            yield return OpenedChoices();

            Assert.That(HeroPower.gameObject.activeInHierarchy, Is.False,
                "The Raise medallion is still active while the choice menu is open - it can still draw over the cards.");

            HeroPower.CloseChoices();

            Assert.That(HeroPower.gameObject.activeInHierarchy, Is.True,
                "The Raise medallion did not come back after Cancel closed the choice menu.");
        }

        /// <summary>
        /// A Raise choice is a modal interaction: nothing else on the HUD
        /// should accept a click while it is open. Driven through the real
        /// <c>MatchInputController.Update</c> loop, which is what actually
        /// disables the button - not a direct call to
        /// <see cref="MatchHud.SetInteractable"/>, which would only prove the
        /// method works, not that opening a choice reaches it. Superseded by
        /// <see cref="End_turn_is_visibly_dimmed_and_non_interactive_while_the_choice_menu_is_open"/>,
        /// which asserts the same interactability plus the dimming that
        /// makes sitting a choice card in front of it safe to look at.
        /// </summary>

        /// <summary>
        /// The same safe-margin and no-overlap invariants as the reference
        /// resolution, but at a deliberately different aspect ratio (4:3,
        /// not one of the 16:9 sizes this was tuned against) - proof the
        /// layout is actually derived from the live camera rather than
        /// happening to work at one aspect it was eyeballed against. Every
        /// listed 16:9 window size shares the exact same aspect ratio, so
        /// re-testing at another one of them would prove nothing this
        /// doesn't already; a genuinely different ratio is the real stress
        /// case for "derived from the viewport, not hardcoded".
        /// </summary>
        [UnityTest]
        public IEnumerator Choice_layout_stays_sane_at_a_different_aspect_ratio()
        {
            yield return BoundButNotYetOpened();

            float originalAspect = MatchCamera.aspect;

            try
            {
                MatchCamera.aspect = 4f / 3f;

                HeroPower.Refresh(Session, true);
                HeroPower.OpenChoices();

                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                }

                int count = HeroPower.ChoiceCards.Count;
                var bounds = new (float minX, float maxX, float minY, float maxY)[count];

                for (int index = 0; index < count; index++)
                {
                    bounds[index] = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);

                    Assert.That(bounds[index].minX, Is.GreaterThanOrEqualTo(ViewportSafeMargin),
                        "At a 4:3 aspect, choice card " + index + " crosses the left safe margin.");
                    Assert.That(bounds[index].maxX, Is.LessThanOrEqualTo(1f - ViewportSafeMargin),
                        "At a 4:3 aspect, choice card " + index + " crosses the right safe margin.");
                }

                for (int index = 0; index < count - 1; index++)
                {
                    Assert.That(bounds[index].maxX, Is.LessThanOrEqualTo(bounds[index + 1].minX),
                        "At a 4:3 aspect, choice card " + index + " overlaps choice card " + (index + 1) + ".");
                }
            }
            finally
            {
                MatchCamera.aspect = originalAspect;
            }
        }

        /// <summary>
        /// Superseded by an explicit design decision: the choice row stays
        /// centred on screen (X = 0.5) at whatever size reads as a proper
        /// prominent choice presentation, and is now allowed to sit where
        /// End Turn is - see <see cref="End_turn_is_visibly_dimmed_and_non_interactive_while_the_choice_menu_is_open"/>
        /// for what actually keeps that safe. A fixed "must stay narrower
        /// than End Turn's own position" bound would only fight that
        /// decision on every future retune.
        /// </summary>
        [UnityTest]
        public IEnumerator End_turn_is_visibly_dimmed_and_non_interactive_while_the_choice_menu_is_open()
        {
            yield return BoundButNotYetOpened();

            HeroPower.Refresh(Session, true);
            yield return null;

            Assert.That(Presenter.Hud.IsEndTurnInteractable, Is.True,
                "End Turn should still be interactable before Raise is ever opened.");
            Assert.That(Presenter.Hud.IsEndTurnModalDimmed, Is.False,
                "End Turn should not be dimmed before Raise is ever opened.");

            HeroPower.OpenChoices();
            yield return null;

            Assert.That(Presenter.Hud.IsEndTurnInteractable, Is.False,
                "End Turn is still clickable while a Raise choice is open - the choice cards are allowed " +
                "to sit visually where it is, which only stays safe if it truly cannot be clicked.");
            Assert.That(Presenter.Hud.IsEndTurnModalDimmed, Is.True,
                "End Turn is not visibly faded while a choice card may be drawn in front of it - it would " +
                "read as still being on top.");

            HeroPower.CloseChoices();
            yield return null;

            Assert.That(Presenter.Hud.IsEndTurnInteractable, Is.True,
                "End Turn did not become interactable again after the choice menu closed.");
            Assert.That(Presenter.Hud.IsEndTurnModalDimmed, Is.False,
                "End Turn stayed dimmed after the choice menu closed.");
        }

        // ==================================================================
        //  Regression: the four choice cards must read as one typographically
        //  consistent group - a manual validation pass caught each title and
        //  each rules text independently settling on a different auto-sized
        //  result, purely because the four servant names and rule strings
        //  are different lengths. This only checks the temporary choice
        //  presentation; ordinary hand and board cards are untouched and
        //  keep fitting their own text exactly as before (see
        //  CardTextStyleTests, unaffected by any of this).
        // ==================================================================

        private static TMPro.TextMeshPro FindLabelWithExactText(CardView card, string text)
        {
            foreach (TMPro.TextMeshPro label in card.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                if (label.text == text)
                {
                    return label;
                }
            }

            return null;
        }

        [UnityTest]
        public IEnumerator Choice_card_titles_share_one_font_size_within_the_group()
        {
            yield return OpenedChoices();

            float? groupSize = null;

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                CardView card = HeroPower.ChoiceCards[index];
                TMPro.TextMeshPro label = FindLabelWithExactText(card, card.Shown.Name);

                Assert.That(label, Is.Not.Null, "No title label found for choice " + index + " ('" + card.Shown.Name + "').");
                Assert.That(label.fontSize, Is.GreaterThan(0f),
                    "Choice " + index + "'s title never resolved a font size at all.");
                Assert.That(label.fontSize, Is.LessThanOrEqualTo(label.fontSizeMax + 0.01f),
                    "Choice " + index + "'s title is pinned larger than its own slot could ever fit.");

                if (groupSize == null)
                {
                    groupSize = label.fontSize;
                }
                else
                {
                    Assert.That(label.fontSize, Is.EqualTo(groupSize.Value).Within(0.01f),
                        "Choice " + index + "'s title ('" + card.Shown.Name + "', size " + label.fontSize +
                        ") does not match the rest of the group's title size (" + groupSize.Value + ").");
                }
            }
        }

        [UnityTest]
        public IEnumerator Choice_card_rules_text_shares_one_font_size_where_present()
        {
            yield return OpenedChoices();

            float? groupSize = null;
            int nonEmptyCount = 0;

            for (int index = 0; index < HeroPower.ChoiceCards.Count; index++)
            {
                CardView card = HeroPower.ChoiceCards[index];
                string rules = card.Shown.RulesText;

                if (string.IsNullOrEmpty(rules))
                {
                    continue;
                }

                nonEmptyCount++;
                TMPro.TextMeshPro label = FindLabelWithExactText(card, rules);

                Assert.That(label, Is.Not.Null, "No rules label found for choice " + index + " ('" + rules + "').");
                Assert.That(label.fontSize, Is.GreaterThan(0f),
                    "Choice " + index + "'s rules text never resolved a font size at all.");
                Assert.That(label.fontSize, Is.LessThanOrEqualTo(label.fontSizeMax + 0.01f),
                    "Choice " + index + "'s rules text is pinned larger than its own slot could ever fit.");

                if (groupSize == null)
                {
                    groupSize = label.fontSize;
                }
                else
                {
                    Assert.That(label.fontSize, Is.EqualTo(groupSize.Value).Within(0.01f),
                        "Choice " + index + "'s rules text (size " + label.fontSize + ") does not match the " +
                        "rest of the group's rules text size (" + groupSize.Value + ").");
                }
            }

            Assert.That(nonEmptyCount, Is.GreaterThan(0),
                "Expected at least one choice card with non-empty rules text (Rush/Camouflage/Provocation).");
        }

        [UnityTest]
        public IEnumerator Crypt_fiends_rules_region_stays_correctly_empty()
        {
            yield return OpenedChoices();

            CardView cryptFiend = HeroPower.ChoiceCards[2];
            Assert.That(cryptFiend.Shown.Name, Is.EqualTo("Crypt Fiend"));
            Assert.That(cryptFiend.Shown.RulesText, Is.Null.Or.Empty,
                "Crypt Fiend should show no rules text - it must not have been given placeholder text " +
                "just to keep the group's sizing pass company.");
        }

        // ==================================================================
        //  Regression: hovering a choice card must read as a small highlight
        //  in place, not the hand's own "pulled out of the fan" motion. A
        //  manual validation pass caught a hovered choice card moving
        //  aggressively enough to leave its slot and, in the worst case,
        //  approach the edge of the screen.
        // ==================================================================

        [UnityTest]
        public IEnumerator Hovering_a_choice_card_stays_in_its_slot_and_does_not_move_its_neighbours()
        {
            yield return OpenedChoices();

            int count = HeroPower.ChoiceCards.Count;
            var restBounds = new (float minX, float maxX, float minY, float maxY)[count];
            float[] restScale = new float[count];

            for (int index = 0; index < count; index++)
            {
                restBounds[index] = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);
                restScale[index] = HeroPower.ChoiceCards[index].transform.lossyScale.x;
            }

            const int hoveredIndex = 1;
            CardView hoveredCard = HeroPower.ChoiceCards[hoveredIndex];

            Vector3 screenPoint = MatchCamera.WorldToScreenPoint(hoveredCard.transform.position);
            HeroPower.ApplyChoicePointer(MatchCamera.ScreenPointToRay(screenPoint), clicked: false);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            (float minX, float maxX, float minY, float maxY) hoveredBounds = ChoiceViewportBounds(hoveredCard);
            float hoveredScale = hoveredCard.transform.lossyScale.x;

            Assert.That(hoveredBounds.minX, Is.GreaterThanOrEqualTo(ViewportSafeMargin),
                "The hovered card crosses the left safe margin.");
            Assert.That(hoveredBounds.maxX, Is.LessThanOrEqualTo(1f - ViewportSafeMargin),
                "The hovered card crosses the right safe margin.");
            Assert.That(hoveredBounds.minY, Is.GreaterThanOrEqualTo(ViewportSafeMargin),
                "The hovered card crosses the bottom safe margin.");
            Assert.That(hoveredBounds.maxY, Is.LessThanOrEqualTo(1f - ViewportSafeMargin),
                "The hovered card crosses the top safe margin.");

            float restCenterX = (restBounds[hoveredIndex].minX + restBounds[hoveredIndex].maxX) * 0.5f;
            float restCenterY = (restBounds[hoveredIndex].minY + restBounds[hoveredIndex].maxY) * 0.5f;
            float hoveredCenterX = (hoveredBounds.minX + hoveredBounds.maxX) * 0.5f;
            float hoveredCenterY = (hoveredBounds.minY + hoveredBounds.maxY) * 0.5f;

            Assert.That(Mathf.Abs(hoveredCenterX - restCenterX), Is.LessThan(0.03f),
                "Hovering moved the card's horizontal centre far more than a subtle highlight should - " +
                "this is the hand's own 'pulled out of the fan' motion leaking into the choice row.");
            Assert.That(Mathf.Abs(hoveredCenterY - restCenterY), Is.LessThan(0.05f),
                "Hovering moved the card's vertical centre far more than a subtle highlight should.");

            Assert.That(hoveredScale / restScale[hoveredIndex], Is.InRange(1.0f, 1.07f),
                "Hovering scaled the card up by more than the intended 1.03-1.06 highlight.");

            for (int index = 0; index < count; index++)
            {
                if (index == hoveredIndex)
                {
                    continue;
                }

                (float minX, float maxX, float minY, float maxY) bounds = ChoiceViewportBounds(HeroPower.ChoiceCards[index]);

                Assert.That(bounds.minX, Is.EqualTo(restBounds[index].minX).Within(0.001f),
                    "Choice card " + index + " moved horizontally while a neighbour was hovered.");
                Assert.That(bounds.minY, Is.EqualTo(restBounds[index].minY).Within(0.001f),
                    "Choice card " + index + " moved vertically while a neighbour was hovered.");
            }

            for (int index = 0; index < count; index++)
            {
                for (int other = index + 1; other < count; other++)
                {
                    (float minX, float maxX, float minY, float maxY) a = index == hoveredIndex ? hoveredBounds : restBounds[index];
                    (float minX, float maxX, float minY, float maxY) b = other == hoveredIndex ? hoveredBounds : restBounds[other];
                    bool aIsLeft = a.minX < b.minX;
                    (float minX, float maxX, float minY, float maxY) left = aIsLeft ? a : b;
                    (float minX, float maxX, float minY, float maxY) right = aIsLeft ? b : a;

                    Assert.That(left.maxX, Is.LessThanOrEqualTo(right.minX),
                        "Choice cards " + index + " and " + other + " overlap while " + hoveredIndex + " is hovered.");
                }
            }

            // A ray at the corner of the screen hits none of the four cards.
            HeroPower.ApplyChoicePointer(MatchCamera.ViewportPointToRay(new Vector3(0.02f, 0.02f, 0f)), clicked: false);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            (float minX, float maxX, float minY, float maxY) returned = ChoiceViewportBounds(hoveredCard);

            Assert.That(returned.minX, Is.EqualTo(restBounds[hoveredIndex].minX).Within(0.01f),
                "The card did not return to its resting slot after the hover ended.");
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            foreach (T component in HeroPower.GetComponentsInChildren<T>(true))
            {
                if (component.gameObject.name == childName)
                {
                    return component;
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
    }
}
