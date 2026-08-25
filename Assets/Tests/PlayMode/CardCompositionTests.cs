using System.Collections;
using CoH.Core.Cards;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// One card view, told to be several different cards.
    ///
    /// This is the test the whole phase exists for. A neutral minion, a spell
    /// and a legendary are three appearances with nothing in common but their
    /// proportions, and in most card games they are three prefabs, three
    /// renderers, or a chain of branches somebody has to extend every time a
    /// new frame is drawn. Here they are one object being handed three plans.
    ///
    /// So what is asserted is not only that each one looks right, but that
    /// nothing was created to make it look right.
    /// </summary>
    public sealed class CardCompositionTests : InteractionTestBase
    {
        private GameObject _stage;
        private CardView _card;
        private CardVisualPainter _painter;

        /// <summary>
        /// Whatever the hand is already using, so the test cannot be composing
        /// against assets the game is not.
        /// </summary>
        private static CardVisualFactory FactoryFromScene()
        {
            CardView any = Object.FindFirstObjectByType<CardView>();
            return any == null ? null : any.Visuals;
        }

        [TearDown]
        public void TearDown()
        {
            if (_stage != null)
            {
                Object.DestroyImmediate(_stage);
                _stage = null;
            }
        }

        /// <summary>A card view outside the hand, so nothing re-binds it underneath.</summary>
        private IEnumerator ACardOfMyOwn()
        {
            yield return LoadMatch();

            CardVisualFactory factory = FactoryFromScene();
            Assert.That(factory, Is.Not.Null, "The scene's cards have no visual factory.");

            _stage = new GameObject("Composed card");
            _painter = _stage.AddComponent<CardVisualPainter>();
            _card = _stage.AddComponent<CardView>();
            _card.UseForTests(factory, _painter);

            yield return null;
        }

        private static CardVisualDescriptor Describe(
            CardType type,
            Rarity rarity = Rarity.Common,
            string name = "Test Soldier") =>
            new CardVisualDescriptor(
                type,
                CardClass.Neutral,
                rarity,
                Tribe.None,
                artwork: null,
                name: name,
                rulesText: "",
                manaCost: 2,
                attack: 2,
                health: 3,
                showsCost: true,
                showsStatistics: type == CardType.Minion || type == CardType.Weapon);

        // ------------------------------------------------------------------

        /// <summary>
        /// Minion, then spell, then legendary minion, on one object. No prefab
        /// is instantiated, and the pool of renderers never grows to hold a
        /// second card's worth of layers.
        /// </summary>
        [UnityTest]
        public IEnumerator One_card_view_becomes_three_different_cards_without_a_second_prefab()
        {
            yield return ACardOfMyOwn();

            int viewsBefore = Object.FindObjectsByType<CardView>(FindObjectsSortMode.None).Length;

            // --- a neutral minion ---------------------------------------
            _card.Show(Describe(CardType.Minion));

            Assert.That(_card.Plan.IsComplete, Is.True, _card.Plan.Describe());
            Assert.That(_card.Plan.Draws(CardVisualSlot.AttackGem), Is.True, "A minion has an attack.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.HealthGem), Is.True);

            Sprite minionFrame = _card.Plan.SpriteIn(CardVisualSlot.Frame);
            Assert.That(minionFrame, Is.Not.Null);

            int pooled = _painter.PooledRendererCount;
            Assert.That(pooled, Is.GreaterThan(0));

            // --- the same object, now a spell ---------------------------
            _card.Show(Describe(CardType.Spell, name: "Test Volley"));

            Assert.That(_card.Plan.IsComplete, Is.True, _card.Plan.Describe());
            Assert.That(_card.Plan.SpriteIn(CardVisualSlot.Frame), Is.Not.SameAs(minionFrame),
                "A spell drew the minion's frame.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.AttackGem), Is.False,
                "A spell kept the minion's attack gem.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.HealthGem), Is.False);

            // --- and now a legendary minion ------------------------------
            _card.Show(Describe(CardType.Minion, Rarity.Legendary, "Test Champion"));

            Assert.That(_card.Plan.IsComplete, Is.True, _card.Plan.Describe());
            Assert.That(_card.Plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(minionFrame),
                "A legendary minion is still a minion.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.EliteFrame), Is.True,
                "A legendary should have its own treatment.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.RarityGem), Is.True);

            // --- nothing was built to achieve any of that ----------------
            Assert.That(
                Object.FindObjectsByType<CardView>(FindObjectsSortMode.None).Length,
                Is.EqualTo(viewsBefore),
                "A second card view appeared, so something was instantiated per variant.");

            Assert.That(_painter.PooledRendererCount, Is.LessThanOrEqualTo(pooled + 4),
                "The renderer pool grew as the card changed kind, rather than being reused.");
        }

        /// <summary>
        /// A basic card and a legendary differ by two layers and nothing else.
        /// Rarity is data, not a prefab.
        /// </summary>
        [UnityTest]
        public IEnumerator Rarity_changes_only_the_layers_rarity_should_change()
        {
            yield return ACardOfMyOwn();

            _card.Show(Describe(CardType.Minion, Rarity.Free));

            Assert.That(_card.Plan.Draws(CardVisualSlot.RarityGem), Is.False,
                "A basic card wears no rarity stone.");
            Assert.That(_card.Plan.Draws(CardVisualSlot.EliteFrame), Is.False);

            Sprite frame = _card.Plan.SpriteIn(CardVisualSlot.Frame);

            _card.Show(Describe(CardType.Minion, Rarity.Epic));

            Assert.That(_card.Plan.Draws(CardVisualSlot.RarityGem), Is.True);
            Assert.That(_card.Plan.Draws(CardVisualSlot.EliteFrame), Is.False,
                "Only a legendary gets the elite treatment.");
            Assert.That(_card.Plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(frame),
                "Rarity changed the frame.");
        }

        /// <summary>
        /// Turning a card face down and back is the same object composing a
        /// different set of layers, and nothing about the card leaks onto its
        /// back.
        /// </summary>
        [UnityTest]
        public IEnumerator A_card_can_be_turned_over_and_back()
        {
            yield return ACardOfMyOwn();

            _card.Show(Describe(CardType.Minion));
            Assert.That(_card.Plan.Draws(CardVisualSlot.Frame), Is.True);

            _card.BindFaceDown();

            Assert.That(_card.IsFaceDown, Is.True);
            Assert.That(_card.Plan.Draws(CardVisualSlot.CardBack), Is.True);
            Assert.That(_card.Plan.Draws(CardVisualSlot.Frame), Is.False,
                "The front of the card survived being turned over.");
            Assert.That(_card.Plan.TextIn(CardVisualTextSlot.Name), Is.Null);

            _card.Show(Describe(CardType.Minion));
            Assert.That(_card.Plan.Draws(CardVisualSlot.Frame), Is.True);
            Assert.That(_card.Plan.Draws(CardVisualSlot.CardBack), Is.False);
        }

        // ------------------------------------------------------------------
        //  In an actual hand
        // ------------------------------------------------------------------

        /// <summary>
        /// The cards a real match deals are composed, complete, and drawn from
        /// the same factory as everything above.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_card_in_a_real_hand_composes_completely()
        {
            yield return LoadMatch();

            int checkedCards = 0;

            foreach (CardView view in Object.FindObjectsByType<CardView>(FindObjectsSortMode.None))
            {
                Assert.That(view.Plan.IsComplete, Is.True,
                    "A card in the hand could not be drawn:\n" + view.Plan.Describe());

                if (!view.IsFaceDown)
                {
                    Assert.That(view.Plan.Draws(CardVisualSlot.Frame), Is.True,
                        "A face up card has no frame.");
                    Assert.That(view.Plan.TextIn(CardVisualTextSlot.Name), Is.Not.Null.And.Not.Empty,
                        "A face up card has no name on it.");
                }

                checkedCards++;
            }

            Assert.That(checkedCards, Is.GreaterThan(0), "The match dealt no cards.");
        }

        /// <summary>
        /// A minion whose numbers change is not composed again. The pictures
        /// were already right, and a match would otherwise be re-resolving a
        /// catalog every time anything moved.
        /// </summary>
        [UnityTest]
        public IEnumerator Re_binding_the_same_card_with_new_numbers_keeps_the_same_pictures()
        {
            yield return ACardOfMyOwn();

            _card.Show(Describe(CardType.Minion));

            Sprite frame = _card.Plan.SpriteIn(CardVisualSlot.Frame);
            int layers = _card.Plan.Layers.Count;
            int pooled = _painter.PooledRendererCount;

            _card.Show(new CardVisualDescriptor(
                CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                artwork: null, name: "Test Soldier", rulesText: "",
                manaCost: 1, attack: 4, health: 5,
                showsCost: true, showsStatistics: true));

            Assert.That(_card.Plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(frame));
            Assert.That(_card.Plan.Layers.Count, Is.EqualTo(layers));
            Assert.That(_painter.PooledRendererCount, Is.EqualTo(pooled));

            Assert.That(_card.Plan.TextIn(CardVisualTextSlot.Attack), Is.EqualTo("4"));
            Assert.That(_card.Plan.TextIn(CardVisualTextSlot.Health), Is.EqualTo("5"));
            Assert.That(_card.Plan.TextIn(CardVisualTextSlot.ManaCost), Is.EqualTo("1"));
        }
    }
}
