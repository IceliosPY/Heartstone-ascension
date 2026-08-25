using System;
using System.Collections.Generic;
using System.Globalization;
using CoH.Core.Commands;
using CoH.Core.Identifiers;

namespace CoH.Core.Diagnostics
{
    /// <summary>Refusing a replay file, with a reason a person can act on.</summary>
    public sealed class ReplayFormatException : Exception
    {
        public ReplayFormatException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Turns a replay into text and back.
    ///
    /// Text on purpose. A replay is a development artefact that gets opened,
    /// read, diffed and pasted into a message far more often than it gets
    /// loaded, and a binary format would be faster at the thing nobody needs it
    /// to be fast at.
    ///
    /// Seeds and entity ids are written as strings rather than numbers, because
    /// JSON numbers are doubles and a 64 bit seed does not survive one intact.
    /// A seed that came back off by one would produce an entirely different
    /// match with nothing to show why.
    /// </summary>
    public static class ReplayFile
    {
        public static string Write(ReplayRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            JsonWriter json = new JsonWriter();

            json.BeginObject();
            json.Write("formatVersion", record.FormatVersion);
            json.Write("createdAtUtc", record.CreatedAtUtc);
            json.Write("initialSource", record.InitialSource.ToString());
            json.Write("seed", record.Seed.ToString(CultureInfo.InvariantCulture));
            json.Write("scenarioId", record.ScenarioId);
            json.Write("catalogFingerprint", record.CatalogFingerprint);
            json.Write("finalStateFingerprint", record.FinalStateFingerprint);

            json.BeginObject("config");
            json.Write("startingHeroHealth", record.Config.StartingHeroHealth);
            json.Write("maxHandSize", record.Config.MaxHandSize);
            json.Write("maxBoardSize", record.Config.MaxBoardSize);
            json.Write("maxManaCrystals", record.Config.MaxManaCrystals);
            json.Write("deckSize", record.Config.DeckSize);
            json.Write("startingPlayerHandSize", record.Config.StartingPlayerHandSize);
            json.Write("secondPlayerHandSize", record.Config.SecondPlayerHandSize);
            json.Write("secondPlayerExtraCard", record.Config.SecondPlayerExtraCard);
            json.EndObject();

            WriteDeck(json, "deckOne", record.DeckOne);
            WriteDeck(json, "deckTwo", record.DeckTwo);

            json.BeginArray("mulliganChoices");

            for (int index = 0; index < record.MulliganChoices.Count; index++)
            {
                ReplayMulligan mulligan = record.MulliganChoices[index];

                json.BeginObject();
                json.Write("player", mulligan.PlayerId.Number);
                json.BeginArray("cardsToReplace");

                for (int card = 0; card < mulligan.CardsToReplace.Count; card++)
                {
                    json.WriteRaw(mulligan.CardsToReplace[card].Value
                        .ToString(CultureInfo.InvariantCulture));
                }

                json.EndArray();
                json.EndObject();
            }

            json.EndArray();

            json.BeginArray("entries");

            for (int index = 0; index < record.Entries.Count; index++)
            {
                WriteEntry(json, record.Entries[index]);
            }

            json.EndArray();
            json.EndObject();

            return json.ToString();
        }

        public static ReplayRecord Read(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            JsonValue root;

            try
            {
                root = JsonValue.Parse(text);
            }
            catch (FormatException error)
            {
                throw new ReplayFormatException("This is not a readable replay file: " + error.Message);
            }

            int version = root["formatVersion"].AsInt(-1);

            if (version != ReplayFormat.CurrentVersion)
            {
                // Named and refused, rather than crashing on the first field
                // that turns out not to be there.
                throw new ReplayFormatException(
                    "Unsupported replay format version: " + version +
                    ". This build reads version " + ReplayFormat.CurrentVersion + ".");
            }

            ReplayConfig config = ReadConfig(root["config"]);

            List<ReplayEntry> entries = new List<ReplayEntry>();
            IReadOnlyList<JsonValue> rawEntries = root["entries"].Items;

            for (int index = 0; index < rawEntries.Count; index++)
            {
                entries.Add(ReadEntry(rawEntries[index]));
            }

            return new ReplayRecord(
                ParseSource(root["initialSource"].AsString("Match")),
                root["seed"].AsULong(),
                ReadDeck(root["deckOne"]),
                ReadDeck(root["deckTwo"]),
                root["scenarioId"].AsString(),
                root["catalogFingerprint"].AsString(),
                config,
                entries,
                version,
                root["createdAtUtc"].AsString(),
                ReadMulligans(root["mulliganChoices"]));
        }

        // ------------------------------------------------------------------

        private static void WriteDeck(JsonWriter json, string name, IReadOnlyList<CardId> deck)
        {
            json.BeginArray(name);

            for (int index = 0; index < deck.Count; index++)
            {
                json.WriteRaw(deck[index].Value);
            }

            json.EndArray();
        }

        private static void WriteEntry(JsonWriter json, ReplayEntry entry)
        {
            json.BeginObject();
            json.Write("sequence", entry.Sequence);

            json.BeginObject("command");
            json.Write("kind", entry.Command.Kind.ToString());
            json.Write("player", entry.Command.PlayerId.Number);
            json.Write("cardInstanceId", entry.Command.CardInstanceId.Value);
            json.Write("boardPosition", entry.Command.BoardPosition);
            json.Write("targetId", entry.Command.TargetId.Value);
            json.Write("attackerId", entry.Command.AttackerId.Value);

            json.BeginArray("mulliganSelection");

            for (int index = 0; index < entry.Command.MulliganSelection.Count; index++)
            {
                json.WriteRaw(entry.Command.MulliganSelection[index].Value
                    .ToString(CultureInfo.InvariantCulture));
            }

            json.EndArray();
            json.EndObject();

            json.Write("accepted", entry.Accepted);
            json.Write("rejectionReason", entry.Reason.ToString());
            json.Write("eventCount", entry.EventCount);
            json.Write("eventFingerprint", entry.EventFingerprint);
            json.Write("stateFingerprint", entry.StateFingerprint);

            json.BeginArray("events");

            for (int index = 0; index < entry.EventLines.Count; index++)
            {
                json.WriteRaw(entry.EventLines[index]);
            }

            json.EndArray();
            json.EndObject();
        }

        private static ReplayEntry ReadEntry(JsonValue raw)
        {
            JsonValue command = raw["command"];

            List<EntityId> mulligan = new List<EntityId>();
            IReadOnlyList<JsonValue> picks = command["mulliganSelection"].Items;

            for (int index = 0; index < picks.Count; index++)
            {
                if (int.TryParse(picks[index].AsString("0"), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int value))
                {
                    mulligan.Add(new EntityId(value));
                }
            }

            ReplayCommand replayCommand = new ReplayCommand(
                ParseKind(command["kind"].AsString()),
                PlayerFromNumber(command["player"].AsInt()),
                new EntityId(command["cardInstanceId"].AsInt()),
                command["boardPosition"].AsInt(),
                new EntityId(command["targetId"].AsInt()),
                new EntityId(command["attackerId"].AsInt()),
                mulligan);

            List<string> lines = new List<string>();
            IReadOnlyList<JsonValue> events = raw["events"].Items;

            for (int index = 0; index < events.Count; index++)
            {
                lines.Add(events[index].AsString());
            }

            return new ReplayEntry(
                raw["sequence"].AsInt(),
                replayCommand,
                raw["accepted"].AsBool(),
                ParseReason(raw["rejectionReason"].AsString()),
                raw["eventCount"].AsInt(),
                raw["eventFingerprint"].AsString(),
                raw["stateFingerprint"].AsString(),
                lines);
        }

        private static ReplayConfig ReadConfig(JsonValue raw)
        {
            if (raw.IsNull)
            {
                throw new ReplayFormatException("The replay file has no config block.");
            }

            return new ReplayConfig(
                raw["startingHeroHealth"].AsInt(30),
                raw["maxHandSize"].AsInt(10),
                raw["maxBoardSize"].AsInt(7),
                raw["maxManaCrystals"].AsInt(10),
                raw["deckSize"].AsInt(30),
                raw["startingPlayerHandSize"].AsInt(3),
                raw["secondPlayerHandSize"].AsInt(4),
                raw["secondPlayerExtraCard"].AsString());
        }

        private static IReadOnlyList<ReplayMulligan> ReadMulligans(JsonValue raw)
        {
            List<ReplayMulligan> mulligans = new List<ReplayMulligan>();
            IReadOnlyList<JsonValue> items = raw.Items;

            for (int index = 0; index < items.Count; index++)
            {
                List<EntityId> cards = new List<EntityId>();
                IReadOnlyList<JsonValue> ids = items[index]["cardsToReplace"].Items;

                for (int card = 0; card < ids.Count; card++)
                {
                    if (int.TryParse(ids[card].AsString("0"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int value))
                    {
                        cards.Add(new EntityId(value));
                    }
                }

                mulligans.Add(new ReplayMulligan(PlayerFromNumber(items[index]["player"].AsInt()), cards));
            }

            return mulligans;
        }

        private static IReadOnlyList<CardId> ReadDeck(JsonValue raw)
        {
            List<CardId> cards = new List<CardId>();
            IReadOnlyList<JsonValue> items = raw.Items;

            for (int index = 0; index < items.Count; index++)
            {
                string id = items[index].AsString();

                if (!string.IsNullOrEmpty(id))
                {
                    cards.Add(new CardId(id));
                }
            }

            return cards;
        }

        private static ReplayInitialSource ParseSource(string text) =>
            string.Equals(text, "Scenario", StringComparison.Ordinal)
                ? ReplayInitialSource.Scenario
                : ReplayInitialSource.Match;

        private static ReplayCommandKind ParseKind(string text) =>
            Enum.TryParse(text, out ReplayCommandKind kind) ? kind : ReplayCommandKind.Unknown;

        private static RejectionReason ParseReason(string text) =>
            Enum.TryParse(text, out RejectionReason reason) ? reason : RejectionReason.None;

        private static PlayerId PlayerFromNumber(int number) => number switch
        {
            1 => PlayerId.One,
            2 => PlayerId.Two,
            _ => PlayerId.None
        };
    }
}
