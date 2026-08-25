using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Recording a match, writing it down, and playing it again in a fresh
    /// engine.
    ///
    /// The replay is a test of the engine rather than a recording of one. The
    /// verifier re-executes the commands and lets the engine produce its own
    /// events; nothing anywhere applies the recorded events to a state. So a
    /// replay that still matches is evidence the engine is deterministic, and
    /// these tests are as much about that as about the file format.
    /// </summary>
    public sealed class ReplayTests
    {
        private static CardCatalog Catalog() => TestFactory.Catalog(
            TestFactory.MinionDefinition(manaCost: 2, attack: 2, health: 3),
            TestFactory.CoinDefinition());

        /// <summary>
        /// Plays a short match against a real engine while recording every
        /// command exactly as the session layer does.
        /// </summary>
        private static ReplayRecorder PlayAndRecord(
            out GameEngine engine, ulong seed = 3UL, bool includeARejection = false)
        {
            CardCatalog catalog = Catalog();
            DeckList deck = TestFactory.Deck();

            engine = new GameEngine(GameConfig.Default, catalog, seed);
            engine.StartMatch(deck, deck);

            ReplayRecorder recorder = ReplayRecorder.ForMatch(
                seed, deck, deck, catalog, GameConfig.Default);

            Submit(engine, recorder, new MulliganCommand(PlayerId.One));
            Submit(engine, recorder, new MulliganCommand(PlayerId.Two));

            if (includeARejection)
            {
                // The player who is not acting tries to end the turn. Refused,
                // and recorded as refused.
                Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer.Opponent));
            }

            // Two turns to reach two mana, then play something.
            Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer));
            Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer));

            PlayFirstAffordableCard(engine, recorder);

            Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer));

            return recorder;
        }

        private static void Submit(GameEngine engine, ReplayRecorder recorder, GameCommand command)
        {
            CommandResult result = engine.Execute(command);
            recorder.Observe(command, result, engine.State);
        }

        private static void PlayFirstAffordableCard(GameEngine engine, ReplayRecorder recorder)
        {
            PlayerId acting = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(acting);

            foreach (CardInstance card in player.Hand)
            {
                if (engine.CanExecute(new PlayCardCommand(acting, card.Id)) == RejectionReason.None)
                {
                    Submit(engine, recorder, new PlayCardCommand(acting, card.Id, 0));
                    return;
                }
            }

            Assert.Fail("Nothing was affordable, so the recording has no PlayCardCommand in it.");
        }

        /// <summary>Records a match that ends with a real attack.</summary>
        private static ReplayRecorder PlayCombatAndRecord(out GameEngine engine)
        {
            CardCatalog catalog = Catalog();
            GameConfig config = GameConfig.Default;

            DebugScenario scenario = DebugScenarios.ReadyCombat;
            GameState state = DebugScenarioBuilder.Build(scenario, catalog, config);
            engine = GameEngine.FromState(state);

            ReplayRecorder recorder = ReplayRecorder.ForScenario(scenario.Id, catalog, config);

            PlayerId acting = state.CurrentPlayer;
            EntityId attacker = state.GetPlayer(acting).Board[0].Id;
            EntityId defender = state.GetPlayer(acting.Opponent).Board[0].Id;

            Submit(engine, recorder, new AttackCommand(acting, attacker, defender));
            Submit(engine, recorder, new EndTurnCommand(acting));

            return recorder;
        }

        // ------------------------------------------------------------------
        //  What a recording holds
        // ------------------------------------------------------------------

        [Test]
        public void A_recording_keeps_its_seed_its_decks_and_its_catalog()
        {
            ReplayRecorder recorder = PlayAndRecord(out GameEngine _, seed: 99UL);
            ReplayRecord record = recorder.Record;

            Assert.That(record.FormatVersion, Is.EqualTo(ReplayFormat.CurrentVersion));
            Assert.That(record.InitialSource, Is.EqualTo(ReplayInitialSource.Match));
            Assert.That(record.Seed, Is.EqualTo(99UL));
            Assert.That(record.DeckOne.Count, Is.EqualTo(30));
            Assert.That(record.DeckTwo.Count, Is.EqualTo(30));
            Assert.That(record.CatalogFingerprint, Is.EqualTo(CatalogFingerprint.Of(Catalog())));
            Assert.That(record.Config.MaxHandSize, Is.EqualTo(GameConfig.Default.MaxHandSize));
        }

        [Test]
        public void Commands_are_numbered_in_the_order_they_were_submitted()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            Assert.That(record.CommandCount, Is.GreaterThan(3));

            for (int index = 0; index < record.Entries.Count; index++)
            {
                Assert.That(record.Entries[index].Sequence, Is.EqualTo(index),
                    "Sequence numbers must be the submission order and nothing else.");
            }
        }

        [Test]
        public void Every_kind_of_command_survives_being_written_and_read_back()
        {
            EntityId card = new EntityId(41);
            EntityId target = new EntityId(22);

            AssertRoundTrips(new MulliganCommand(PlayerId.One, card, target));
            AssertRoundTrips(new EndTurnCommand(PlayerId.Two));
            AssertRoundTrips(new PlayCardCommand(PlayerId.One, card, 3, target));
            AssertRoundTrips(new AttackCommand(PlayerId.Two, card, target));
        }

        private static void AssertRoundTrips(GameCommand original)
        {
            ReplayCommand captured = ReplayCommand.From(original);
            GameCommand rebuilt = captured.ToCommand();

            Assert.That(rebuilt.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(rebuilt.PlayerId, Is.EqualTo(original.PlayerId));

            switch (original)
            {
                case MulliganCommand mulligan:
                    Assert.That(((MulliganCommand)rebuilt).CardsToReplace,
                        Is.EqualTo(mulligan.CardsToReplace));
                    break;

                case PlayCardCommand play:
                    PlayCardCommand rebuiltPlay = (PlayCardCommand)rebuilt;
                    Assert.That(rebuiltPlay.CardInstanceId, Is.EqualTo(play.CardInstanceId));
                    Assert.That(rebuiltPlay.BoardPosition, Is.EqualTo(play.BoardPosition));
                    Assert.That(rebuiltPlay.TargetId, Is.EqualTo(play.TargetId));
                    break;

                case AttackCommand attack:
                    AttackCommand rebuiltAttack = (AttackCommand)rebuilt;
                    Assert.That(rebuiltAttack.AttackerId, Is.EqualTo(attack.AttackerId));
                    Assert.That(rebuiltAttack.TargetId, Is.EqualTo(attack.TargetId));
                    break;
            }
        }

        /// <summary>
        /// A refused command is part of the recording. "The engine refused
        /// something it should have taken" is a bug, and a replay that dropped
        /// the attempt could never show it.
        /// </summary>
        [Test]
        public void A_refused_command_is_recorded_with_its_reason()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _, includeARejection: true).Record;

            ReplayEntry refused = null;

            foreach (ReplayEntry entry in record.Entries)
            {
                if (!entry.Accepted)
                {
                    refused = entry;
                }
            }

            Assert.That(refused, Is.Not.Null, "The recording was supposed to contain a refusal.");
            Assert.That(refused.Reason, Is.EqualTo(RejectionReason.NotYourTurn));
            Assert.That(refused.EventCount, Is.Zero, "A refused command produces no events.");
        }

        // ------------------------------------------------------------------
        //  The file
        // ------------------------------------------------------------------

        [Test]
        public void A_recording_survives_being_written_to_text_and_read_back()
        {
            ReplayRecord original = PlayAndRecord(out GameEngine _, seed: 5UL, includeARejection: true).Record;

            string text = ReplayFile.Write(original);
            ReplayRecord reloaded = ReplayFile.Read(text);

            Assert.That(reloaded.Seed, Is.EqualTo(original.Seed));
            Assert.That(reloaded.FormatVersion, Is.EqualTo(original.FormatVersion));
            Assert.That(reloaded.InitialSource, Is.EqualTo(original.InitialSource));
            Assert.That(reloaded.CatalogFingerprint, Is.EqualTo(original.CatalogFingerprint));
            Assert.That(reloaded.CommandCount, Is.EqualTo(original.CommandCount));
            Assert.That(reloaded.DeckOne, Is.EqualTo(original.DeckOne));

            for (int index = 0; index < original.Entries.Count; index++)
            {
                ReplayEntry before = original.Entries[index];
                ReplayEntry after = reloaded.Entries[index];

                Assert.That(after.Sequence, Is.EqualTo(before.Sequence));
                Assert.That(after.Command.Kind, Is.EqualTo(before.Command.Kind));
                Assert.That(after.Command.Describe(), Is.EqualTo(before.Command.Describe()));
                Assert.That(after.Accepted, Is.EqualTo(before.Accepted));
                Assert.That(after.Reason, Is.EqualTo(before.Reason));
                Assert.That(after.StateFingerprint, Is.EqualTo(before.StateFingerprint));
                Assert.That(after.EventLines, Is.EqualTo(before.EventLines));
            }
        }

        /// <summary>A seed is 64 bits, and JSON numbers are not.</summary>
        [Test]
        public void A_large_seed_survives_the_file_intact()
        {
            CardCatalog catalog = Catalog();
            DeckList deck = TestFactory.Deck();

            ulong awkward = 18446744073709551615UL;

            ReplayRecord record = ReplayRecorder
                .ForMatch(awkward, deck, deck, catalog, GameConfig.Default).Record;

            Assert.That(ReplayFile.Read(ReplayFile.Write(record)).Seed, Is.EqualTo(awkward));
        }

        [Test]
        public void An_unknown_format_version_is_refused_by_name()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;
            string text = ReplayFile.Write(record).Replace("\"formatVersion\": 1", "\"formatVersion\": 4");

            ReplayFormatException error = Assert.Throws<ReplayFormatException>(() => ReplayFile.Read(text));

            Assert.That(error.Message, Does.Contain("4"));
            Assert.That(error.Message, Does.Contain("Unsupported replay format version"));
        }

        // ------------------------------------------------------------------
        //  Verifying
        // ------------------------------------------------------------------

        [Test]
        public void A_recorded_match_replays_identically_in_a_fresh_engine()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine original).Record;

            ReplayVerificationResult result = ReplayVerifier.Verify(record, Catalog());

            Assert.That(result.Success, Is.True, result.Describe());
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.None));
            Assert.That(result.CommandsChecked, Is.EqualTo(record.CommandCount));

            // And the fresh engine really did arrive at the same place.
            GameEngine replayed = ReplayVerifier.BuildEngine(record, Catalog());

            foreach (ReplayEntry entry in record.Entries)
            {
                replayed.Execute(entry.Command.ToCommand());
            }

            Assert.That(StateFingerprint.Of(replayed.State), Is.EqualTo(StateFingerprint.Of(original.State)));
        }

        /// <summary>
        /// The opening exchange is settled by the host before anyone can act, so
        /// it never becomes a recorded command. A replay that did not carry it
        /// would start a fresh match still waiting for a mulligan and refuse the
        /// very first thing it was asked to do.
        /// </summary>
        [Test]
        public void A_host_settled_mulligan_travels_in_the_header_and_is_applied_again()
        {
            CardCatalog catalog = Catalog();
            DeckList deck = TestFactory.Deck();

            GameEngine engine = new GameEngine(GameConfig.Default, catalog, 17UL);
            engine.StartMatch(deck, deck);

            // Settled by the host, exactly as the bootstrap does it.
            engine.Execute(new MulliganCommand(PlayerId.One));
            engine.Execute(new MulliganCommand(PlayerId.Two));

            ReplayMulligan[] mulligans =
            {
                new ReplayMulligan(PlayerId.One, System.Array.Empty<EntityId>()),
                new ReplayMulligan(PlayerId.Two, System.Array.Empty<EntityId>())
            };

            ReplayRecorder recorder = ReplayRecorder.ForMatch(
                17UL, deck, deck, catalog, GameConfig.Default, string.Empty, mulligans);

            Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer));
            Submit(engine, recorder, new EndTurnCommand(engine.State.CurrentPlayer));

            ReplayRecord record = recorder.Record;

            Assert.That(record.MulliganChoices.Count, Is.EqualTo(2));

            ReplayVerificationResult result = ReplayVerifier.Verify(record, Catalog());

            Assert.That(result.Success, Is.True, result.Describe());

            // And it survives the file.
            ReplayRecord reloaded = ReplayFile.Read(ReplayFile.Write(record));

            Assert.That(reloaded.MulliganChoices.Count, Is.EqualTo(2));
            Assert.That(ReplayVerifier.Verify(reloaded, Catalog()).Success, Is.True);
        }

        [Test]
        public void A_recorded_combat_replays_identically()
        {
            ReplayRecord record = PlayCombatAndRecord(out GameEngine _).Record;

            Assert.That(record.InitialSource, Is.EqualTo(ReplayInitialSource.Scenario));
            Assert.That(record.ScenarioId, Is.EqualTo(DebugScenarios.ReadyCombatId));

            ReplayVerificationResult result = ReplayVerifier.Verify(record, Catalog());

            Assert.That(result.Success, Is.True, result.Describe());
        }

        [Test]
        public void Verifying_the_same_replay_twice_gives_the_same_answer()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            ReplayVerificationResult first = ReplayVerifier.Verify(record, Catalog());
            ReplayVerificationResult second = ReplayVerifier.Verify(record, Catalog());

            Assert.That(first.Success, Is.True, first.Describe());
            Assert.That(second.Success, Is.True, second.Describe());
            Assert.That(second.CommandsChecked, Is.EqualTo(first.CommandsChecked));
            Assert.That(second.Describe(), Is.EqualTo(first.Describe()));
        }

        /// <summary>Verifying must never disturb the match it was recorded from.</summary>
        [Test]
        public void Verifying_leaves_the_original_match_untouched()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine original).Record;

            string before = StateFingerprint.Of(original.State);
            ReplayVerifier.Verify(record, Catalog());

            Assert.That(StateFingerprint.Of(original.State), Is.EqualTo(before));
        }

        [Test]
        public void A_re_tuned_catalog_is_reported_rather_than_replayed()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            CardCatalog retuned = TestFactory.Catalog(
                TestFactory.MinionDefinition(manaCost: 3, attack: 3, health: 3),
                TestFactory.CoinDefinition());

            ReplayVerificationResult result = ReplayVerifier.Verify(record, retuned);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.CatalogMismatch));
            Assert.That(result.Describe(), Does.Contain("CatalogMismatch"));
        }

        [Test]
        public void An_unsupported_format_version_is_reported_rather_than_replayed()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            ReplayRecord fromTheFuture = new ReplayRecord(
                record.InitialSource, record.Seed, record.DeckOne, record.DeckTwo,
                record.ScenarioId, record.CatalogFingerprint, record.Config,
                record.Entries, formatVersion: 4);

            ReplayVerificationResult result = ReplayVerifier.Verify(fromTheFuture, Catalog());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.ReplayFormatMismatch));
        }

        /// <summary>
        /// A tampered recording has to be caught at the exact command that no
        /// longer matches, and the check has to stop there: one divergence
        /// makes every later command land in a different position, and a
        /// hundred reports are worth less than the first one.
        /// </summary>
        [Test]
        public void A_divergence_stops_at_the_first_command_that_disagrees()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            int tampered = 2;
            List<ReplayEntry> entries = new List<ReplayEntry>(record.Entries);
            ReplayEntry original = entries[tampered];

            entries[tampered] = new ReplayEntry(
                original.Sequence, original.Command, original.Accepted, original.Reason,
                original.EventCount, original.EventFingerprint,
                "0000000000000000",
                original.EventLines);

            ReplayRecord broken = new ReplayRecord(
                record.InitialSource, record.Seed, record.DeckOne, record.DeckTwo,
                record.ScenarioId, record.CatalogFingerprint, record.Config, entries);

            ReplayVerificationResult result = ReplayVerifier.Verify(broken, Catalog());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.StateFingerprintMismatch));
            Assert.That(result.DivergenceSequence, Is.EqualTo(tampered),
                "The report has to name the first command that disagreed.");
            Assert.That(result.CommandsChecked, Is.EqualTo(tampered),
                "Checking has to stop there rather than carry on into noise.");
            Assert.That(result.Expected, Is.EqualTo("0000000000000000"));
            Assert.That(result.Describe(), Does.Contain("#" + tampered));
        }

        [Test]
        public void An_event_that_changed_is_reported_as_an_event_divergence()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            int tampered = FindFirstAcceptedWithEvents(record);
            List<ReplayEntry> entries = new List<ReplayEntry>(record.Entries);
            ReplayEntry original = entries[tampered];

            List<string> lines = new List<string>(original.EventLines);
            lines[0] = "SomethingElse";

            entries[tampered] = new ReplayEntry(
                original.Sequence, original.Command, original.Accepted, original.Reason,
                original.EventCount, original.EventFingerprint, original.StateFingerprint, lines);

            ReplayRecord broken = new ReplayRecord(
                record.InitialSource, record.Seed, record.DeckOne, record.DeckTwo,
                record.ScenarioId, record.CatalogFingerprint, record.Config, entries);

            ReplayVerificationResult result = ReplayVerifier.Verify(broken, Catalog());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.EventMismatch));
            Assert.That(result.DivergenceSequence, Is.EqualTo(tampered));
            Assert.That(result.Expected, Does.Contain("SomethingElse"));
        }

        [Test]
        public void A_command_that_is_now_refused_is_reported_as_a_result_divergence()
        {
            ReplayRecord record = PlayAndRecord(out GameEngine _).Record;

            int tampered = 1;
            List<ReplayEntry> entries = new List<ReplayEntry>(record.Entries);
            ReplayEntry original = entries[tampered];

            entries[tampered] = new ReplayEntry(
                original.Sequence, original.Command, accepted: false, RejectionReason.NotYourTurn,
                0, string.Empty, original.StateFingerprint, new string[0]);

            ReplayRecord broken = new ReplayRecord(
                record.InitialSource, record.Seed, record.DeckOne, record.DeckTwo,
                record.ScenarioId, record.CatalogFingerprint, record.Config, entries);

            ReplayVerificationResult result = ReplayVerifier.Verify(broken, Catalog());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.CommandResultMismatch));
            Assert.That(result.DivergenceSequence, Is.EqualTo(tampered));
        }

        private static int FindFirstAcceptedWithEvents(ReplayRecord record)
        {
            for (int index = 0; index < record.Entries.Count; index++)
            {
                if (record.Entries[index].Accepted && record.Entries[index].EventLines.Count > 0)
                {
                    return index;
                }
            }

            Assert.Fail("The recording contains no accepted command with events.");
            return -1;
        }
    }
}
