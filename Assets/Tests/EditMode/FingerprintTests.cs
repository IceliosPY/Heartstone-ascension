using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Fingerprints: the same match always describes itself the same way, and a
    /// match that differs in any way the rules can see describes itself
    /// differently.
    ///
    /// Both halves matter. One that changed when nothing had would raise false
    /// alarms until it was ignored; one that stayed the same when something had
    /// would let a real divergence through. So these check what must move and
    /// what must not, and never that a particular hash equals a particular
    /// string, which would only pin the format down for no benefit.
    /// </summary>
    public sealed class FingerprintTests
    {
        // ------------------------------------------------------------------
        //  State
        // ------------------------------------------------------------------

        [Test]
        public void The_same_match_played_twice_fingerprints_the_same()
        {
            GameEngine first = TestFactory.StartedMatch(seed: 42UL);
            GameEngine second = TestFactory.StartedMatch(seed: 42UL);

            Assert.That(StateFingerprint.Of(second.State), Is.EqualTo(StateFingerprint.Of(first.State)));
            Assert.That(StateFingerprint.Describe(second.State), Is.EqualTo(StateFingerprint.Describe(first.State)));
        }

        [Test]
        public void A_different_seed_fingerprints_differently()
        {
            GameEngine first = TestFactory.StartedMatch(seed: 1UL);
            GameEngine second = TestFactory.StartedMatch(seed: 2UL);

            Assert.That(StateFingerprint.Of(second.State), Is.Not.EqualTo(StateFingerprint.Of(first.State)));
        }

        [Test]
        public void Hurting_a_hero_changes_the_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            string before = StateFingerprint.Of(engine.State);

            TestFactory.Damage(engine, TestFactory.EnemyHero(engine).Id, 3);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        [Test]
        public void Changing_mana_changes_the_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            string before = StateFingerprint.Of(engine.State);

            TestFactory.GiveMana(engine, engine.State.CurrentPlayer, 3);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// Board order is game state, not a drawing detail: it decides where a
        /// summon lands and which minion an effect reaches.
        /// </summary>
        [Test]
        public void Reordering_a_board_changes_the_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId acting = engine.State.CurrentPlayer;

            TestFactory.PutMinionOnBoard(engine, acting);
            TestFactory.PutMinionOnBoard(engine, acting);

            string before = StateFingerprint.Of(engine.State);

            Zone<Minion> board = engine.State.GetPlayer(acting).Board;
            Minion first = board.RemoveAt(0);
            board.TryInsert(1, first);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        /// <summary>Deck order decides what is drawn next, so it counts too.</summary>
        [Test]
        public void Reordering_a_deck_changes_the_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Player player = engine.State.GetPlayer(PlayerId.One);

            string before = StateFingerprint.Of(engine.State);

            CardInstance top = player.Deck.RemoveAt(0);
            player.Deck.TryInsert(1, top);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        [Test]
        public void Damaging_a_minion_changes_the_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, engine.State.CurrentPlayer);

            string before = StateFingerprint.Of(engine.State);

            minion.Damage += 1;

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// Two matches holding the same cards but having created their entities
        /// in a different order are genuinely different matches: an id is what
        /// every command and event refers to.
        /// </summary>
        [Test]
        public void Different_entity_ids_fingerprint_differently()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId acting = engine.State.CurrentPlayer;

            TestFactory.PutMinionOnBoard(engine, acting);
            string before = StateFingerprint.Of(engine.State);

            // Same card, same position, new entity.
            engine.State.GetPlayer(acting).Board.RemoveAt(0);
            TestFactory.PutMinionOnBoard(engine, acting);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// Nothing in the description is read out of a dictionary. Building the
        /// same match repeatedly has to give the same answer every time, and a
        /// hash table asked to enumerate itself gives no such promise.
        /// </summary>
        [Test]
        public void The_fingerprint_does_not_depend_on_internal_hash_ordering()
        {
            string first = StateFingerprint.Describe(TestFactory.StartedMatch(seed: 7UL).State);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Assert.That(
                    StateFingerprint.Describe(TestFactory.StartedMatch(seed: 7UL).State),
                    Is.EqualTo(first),
                    "The description changed between two builds of the same match.");
            }
        }

        [Test]
        public void The_readable_dump_names_both_players_and_the_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            string dump = StateDump.Readable(engine.State);

            Assert.That(dump, Does.Contain("P1"));
            Assert.That(dump, Does.Contain("P2"));
            Assert.That(dump, Does.Contain("Turn:"));
            Assert.That(dump, Does.Contain("Hand:"));
            Assert.That(dump, Does.Contain("Board:"));
            Assert.That(dump, Does.Contain(StateFingerprint.Of(engine.State)));
        }

        // ------------------------------------------------------------------
        //  Catalog
        // ------------------------------------------------------------------

        [Test]
        public void The_same_catalog_fingerprints_the_same_whatever_order_it_was_built_in()
        {
            CardDefinition soldier = TestFactory.MinionDefinition();
            CardDefinition coin = TestFactory.CoinDefinition();

            CardCatalog forwards = new CardCatalog(new List<CardDefinition> { soldier, coin });
            CardCatalog backwards = new CardCatalog(new List<CardDefinition> { coin, soldier });

            Assert.That(CatalogFingerprint.Of(backwards), Is.EqualTo(CatalogFingerprint.Of(forwards)));
        }

        [Test]
        public void Re_tuning_a_card_changes_the_catalog_fingerprint()
        {
            CardCatalog before = new CardCatalog(new List<CardDefinition>
            {
                TestFactory.MinionDefinition(manaCost: 2, attack: 2, health: 3)
            });

            CardCatalog after = new CardCatalog(new List<CardDefinition>
            {
                TestFactory.MinionDefinition(manaCost: 3, attack: 3, health: 3)
            });

            Assert.That(CatalogFingerprint.Of(after), Is.Not.EqualTo(CatalogFingerprint.Of(before)));
        }

        /// <summary>
        /// The name and the rules text are written for a person; nothing parses
        /// them. Rewording a card must not invalidate a replay of a match it
        /// was in, any more than redrawing it would.
        /// </summary>
        [Test]
        public void Rewording_a_card_leaves_the_gameplay_fingerprint_alone()
        {
            CardCatalog before = new CardCatalog(new List<CardDefinition>
            {
                new CardDefinition(
                    new CardId("test_soldier"), "Test Soldier", CardType.Minion,
                    manaCost: 2, attack: 2, health: 3, text: "A plain soldier.")
            });

            CardCatalog after = new CardCatalog(new List<CardDefinition>
            {
                new CardDefinition(
                    new CardId("test_soldier"), "Veteran Soldier", CardType.Minion,
                    manaCost: 2, attack: 2, health: 3, text: "Completely rewritten flavour.")
            });

            Assert.That(CatalogFingerprint.Of(after), Is.EqualTo(CatalogFingerprint.Of(before)));
        }

        [Test]
        public void Making_a_card_uncollectible_changes_the_catalog_fingerprint()
        {
            CardCatalog before = new CardCatalog(new List<CardDefinition>
            {
                new CardDefinition(new CardId("token"), "Token", CardType.Minion, 1, 1, 1)
            });

            CardCatalog after = new CardCatalog(new List<CardDefinition>
            {
                new CardDefinition(new CardId("token"), "Token", CardType.Minion, 1, 1, 1, collectible: false)
            });

            Assert.That(CatalogFingerprint.Of(after), Is.Not.EqualTo(CatalogFingerprint.Of(before)));
        }

        // ------------------------------------------------------------------
        //  Events
        // ------------------------------------------------------------------

        [Test]
        public void The_same_events_fingerprint_the_same_and_different_ones_do_not()
        {
            GameEngine first = TestFactory.StartedMatch(seed: 11UL);
            GameEngine second = TestFactory.StartedMatch(seed: 11UL);

            var one = TestFactory.EndTurn(first).Events;
            var two = TestFactory.EndTurn(second).Events;

            Assert.That(EventFingerprint.Of(two), Is.EqualTo(EventFingerprint.Of(one)));

            var three = TestFactory.EndTurn(first).Events;

            Assert.That(EventFingerprint.Of(three), Is.Not.EqualTo(EventFingerprint.Of(one)),
                "A different turn should not report the same events.");
        }

        [Test]
        public void Every_event_a_turn_produces_is_described_rather_than_unknown()
        {
            GameEngine engine = TestFactory.StartedMatch();

            string described = EventFingerprint.Describe(TestFactory.EndTurn(engine).Events);

            Assert.That(described, Does.Not.Contain("UNKNOWN"),
                "An event type nobody taught the fingerprint about weakens every comparison.");
        }
    }
}
