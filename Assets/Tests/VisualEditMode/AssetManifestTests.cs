using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The download manifest, checked against what the composer can actually
    /// use.
    ///
    /// The manifest is two things at once: the allowlist a fetcher is allowed
    /// to download, and the mapping that fills the catalog. Both halves can be
    /// wrong in ways nobody notices until a card is missing a frame, so both
    /// are checked here — every slot has to be a real slot, every constraint a
    /// real value, and every named slot one the recipe actually draws.
    ///
    /// It reads the file rather than a copy of it. A test with its own idea of
    /// what the manifest says would pass while the real one was broken.
    /// </summary>
    public sealed class AssetManifestTests
    {
        private const string ManifestPath = "Tools/HearthCards/hearthcards-assets.json";

        private static string Text()
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string path = Path.Combine(root, ManifestPath);

            Assert.That(File.Exists(path), Is.True, "No manifest at " + ManifestPath);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// JsonUtility cannot hand back a list of loosely typed records, so the
        /// few fields these tests care about are read by hand. The importer
        /// parses it properly; this only has to be able to see it.
        ///
        /// Split on the id rather than matched as a brace-delimited block: an
        /// entry now carries a nested rectangle, and a pattern that assumed a
        /// flat object silently stopped seeing any entry at all — which is a
        /// test that passes by finding nothing.
        /// </summary>
        private static List<Dictionary<string, string>> Entries()
        {
            string text = Text();

            int start = text.IndexOf("\"entries\"", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(-1), "The manifest has no entries.");

            string body = text.Substring(start);
            List<Dictionary<string, string>> entries = new List<Dictionary<string, string>>();

            MatchCollection starts = Regex.Matches(body, "\"id\"\\s*:");

            for (int index = 0; index < starts.Count; index++)
            {
                int from = starts[index].Index;
                int to = index + 1 < starts.Count ? starts[index + 1].Index : body.Length;

                Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (Match field in Regex.Matches(
                    body.Substring(from, to - from),
                    "\"(\\w+)\"\\s*:\\s*(?:\"([^\"]*)\"|(null)|(-?\\d+))"))
                {
                    string key = field.Groups[1].Value;

                    // First value wins, so a nested rectangle's width cannot
                    // overwrite anything the entry itself declared.
                    if (!fields.ContainsKey(key))
                    {
                        fields[key] = field.Groups[2].Success
                            ? field.Groups[2].Value
                            : field.Groups[4].Success ? field.Groups[4].Value : null;
                    }
                }

                if (fields.ContainsKey("id") && fields.ContainsKey("slot"))
                {
                    entries.Add(fields);
                }
            }

            Assert.That(entries, Is.Not.Empty, "No entries could be read out of the manifest.");
            return entries;
        }

        /// <summary>
        /// The manifest is meant to hold every component, so a parser that
        /// quietly found two of them would make every test below vacuous.
        /// </summary>
        [Test]
        public void The_whole_manifest_is_read_not_a_fragment_of_it()
        {
            int declared = Regex.Matches(Text(), "\"filename\"\\s*:").Count;

            Assert.That(Entries().Count, Is.EqualTo(declared),
                "The manifest declares " + declared + " components and the parser found fewer.");
        }

        [Test]
        public void Every_entry_names_a_real_slot()
        {
            foreach (Dictionary<string, string> entry in Entries())
            {
                Assert.That(
                    Enum.TryParse(entry["slot"], out CardVisualSlot slot) && slot != CardVisualSlot.None,
                    Is.True,
                    entry["id"] + " names slot '" + entry["slot"] + "', which does not exist.");
            }
        }

        [Test]
        public void Every_constraint_is_a_value_the_engine_knows()
        {
            foreach (Dictionary<string, string> entry in Entries())
            {
                Check<CardType>(entry, "cardType");
                Check<CardClass>(entry, "cardClass");
                Check<Rarity>(entry, "rarity");
            }
        }

        private static void Check<T>(Dictionary<string, string> entry, string field) where T : struct
        {
            if (!entry.TryGetValue(field, out string value) || string.IsNullOrEmpty(value))
            {
                // Not constrained, which is the normal case for a shared
                // component: one mana gem serves every card there will ever be.
                return;
            }

            Assert.That(Enum.TryParse(value, out T _), Is.True,
                entry["id"] + " constrains " + field + " to '" + value +
                "', which is not a value of " + typeof(T).Name + ".");
        }

        [Test]
        public void No_entry_is_listed_twice()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Dictionary<string, string> entry in Entries())
            {
                Assert.That(seen.Add(entry["id"]), Is.True,
                    "'" + entry["id"] + "' is listed twice, so which one wins depends on file order.");
            }
        }

        /// <summary>
        /// A component whose slot no layer draws would be downloaded, imported,
        /// and never appear on a card. Worth catching at the manifest rather
        /// than by wondering why a picture is not showing up.
        /// </summary>
        [Test]
        public void Every_slot_in_the_manifest_is_one_the_recipe_draws()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null);

            CardVisualRecipeAsset recipe = factory.RecipeFor(CardVisualStyle.Default);
            Assert.That(recipe, Is.Not.Null);

            HashSet<CardVisualSlot> drawn = new HashSet<CardVisualSlot>();

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                if (recipe.Layers[index] != null)
                {
                    drawn.Add(recipe.Layers[index].slot);
                }
            }

            foreach (Dictionary<string, string> entry in Entries())
            {
                if (Enum.TryParse(entry["slot"], out CardVisualSlot slot))
                {
                    Assert.That(drawn, Contains.Item(slot),
                        entry["id"] + " fills the " + slot + " slot, which no layer draws.");
                }
            }
        }

        /// <summary>
        /// The manifest maps components to kinds of card. The moment it could
        /// map one to a particular card, the composer's whole guarantee would be
        /// gone, and it would be gone somewhere nobody thinks to look.
        /// </summary>
        [Test]
        public void The_manifest_cannot_name_a_card()
        {
            foreach (Dictionary<string, string> entry in Entries())
            {
                Assert.That(entry.ContainsKey("cardId"), Is.False,
                    entry["id"] + " names a card. Components belong to kinds of card, never to one card.");
            }
        }

        [Test]
        public void Every_entry_has_somewhere_to_go_and_something_to_fetch()
        {
            foreach (Dictionary<string, string> entry in Entries())
            {
                Assert.That(entry.TryGetValue("filename", out string filename), Is.True,
                    entry["id"] + " has no filename.");
                Assert.That(filename, Is.Not.Empty);

                Assert.That(entry.TryGetValue("url", out string url), Is.True,
                    entry["id"] + " has no url, so nothing could fetch it.");
                Assert.That(url, Does.StartWith("https://"),
                    entry["id"] + " is not fetched over https.");
                Assert.That(url, Does.EndWith(filename),
                    entry["id"] + " downloads a file with a different name than it records.");
            }
        }
    }
}
