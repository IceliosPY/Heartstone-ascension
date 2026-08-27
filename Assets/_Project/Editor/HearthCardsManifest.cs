using System;
using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>One component: where it came from, and where it goes.</summary>
    [Serializable]
    public sealed class HearthCardsEntry
    {
        public string id;
        public string filename;
        public string url;
        public string status;
        public string category;
        public string purpose;
        public string slot;
        public string cardType;

        // The manifest calls this cardClass rather than class, because
        // JsonUtility matches field names exactly and "class" is a keyword it
        // could never be mapped onto.
        public string cardClass;
        public string rarity;

        public HearthCardsRect sourceRect;

        public bool TryReadSlot(out CardVisualSlot value) =>
            Enum.TryParse(slot, out value) && value != CardVisualSlot.None;

        public bool TryReadType(out CardType value) =>
            Enum.TryParse(cardType, out value) && value != CardType.None;

        /// <summary>
        /// The constraints this component applies under, in the form the
        /// catalog matches on. An empty field is not a constraint, which is how
        /// one mana gem serves every card and one frame serves one type.
        /// </summary>
        public CardVisualMatch Match()
        {
            bool hasType = TryReadType(out CardType type);
            bool hasClass = Enum.TryParse(cardClass, out CardClass ofClass);
            bool hasRarity = Enum.TryParse(rarity, out Rarity ofRarity);

            return new CardVisualMatch
            {
                constrainType = hasType,
                type = hasType ? type : CardType.None,
                constrainClass = hasClass,
                cardClass = hasClass ? ofClass : CardClass.Neutral,
                constrainRarity = hasRarity,
                rarity = hasRarity ? ofRarity : Rarity.Free,
                constrainTribe = false,
                style = default
            };
        }
    }

    /// <summary>Where the renderer draws a component on the 800 by 1100 canvas.</summary>
    [Serializable]
    public struct HearthCardsRect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public bool Exists => width > 0f && height > 0f;

        public Rect ToRect() => new Rect(x, y, width, height);
    }

    [Serializable]
    public sealed class HearthCardsManifestFile
    {
        public string rawFolder;
        public string importedFolder;
        public HearthCardsEntry[] entries;
    }

    /// <summary>
    /// The downloaded components, and what the renderer does with them.
    ///
    /// One reader, used by everything that needs the manifest: the importer
    /// that fills the catalog, and the setup that builds the recipe. The
    /// measured rectangles live in the manifest because that is where they were
    /// established; copying them into a second file by hand is how two files
    /// start disagreeing about where a gem goes.
    ///
    /// The coordinates need no conversion. The renderer composes on an 800 by
    /// 1100 canvas with the origin at the top left and y running down, which is
    /// exactly the card space this project has authored in since its layout was
    /// first measured, and <see cref="CardCanvas"/> is what turns that into
    /// world units. A component's image is also exactly the size of its
    /// rectangle — every one of the twenty-four, checked — so nothing is ever
    /// scaled on the way in.
    /// </summary>
    public static class HearthCardsManifest
    {
        public const string Path = "Tools/HearthCards/hearthcards-assets.json";

        private const string DefaultImportedFolder = "Assets/ThirdParty/HearthCards/Imported";

        public static bool TryLoad(out HearthCardsManifestFile manifest)
        {
            manifest = null;

            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string path = System.IO.Path.Combine(root, Path);

            if (!File.Exists(path))
            {
                Debug.LogError("No asset manifest at " + Path);
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<HearthCardsManifestFile>(File.ReadAllText(path));
            }
            catch (Exception error)
            {
                Debug.LogError("The asset manifest could not be read: " + error.Message);
                return false;
            }

            if (manifest?.entries == null || manifest.entries.Length == 0)
            {
                Debug.LogError("The asset manifest lists nothing.");
                return false;
            }

            return true;
        }

        /// <summary>Loads it, or hands back an empty one rather than throwing.</summary>
        public static HearthCardsManifestFile LoadOrEmpty() =>
            TryLoad(out HearthCardsManifestFile manifest)
                ? manifest
                : new HearthCardsManifestFile { entries = Array.Empty<HearthCardsEntry>() };

        public static string ImportedPathOf(HearthCardsManifestFile manifest, HearthCardsEntry entry)
        {
            string folder = manifest == null || string.IsNullOrEmpty(manifest.importedFolder)
                ? DefaultImportedFolder
                : manifest.importedFolder;

            return folder + "/" + System.IO.Path.GetFileNameWithoutExtension(entry.filename) + ".png";
        }

        /// <summary>
        /// Where a slot's component is drawn for a card of this type, or
        /// nothing.
        ///
        /// An entry constrained to a type wins over an unconstrained one, which
        /// is the same preference the catalog uses. A shared component such as
        /// the mana gem has one rectangle for every card, and a frame has one
        /// per type — which is the whole reason the recipe needs this rather
        /// than a single rectangle per slot.
        /// </summary>
        public static bool TryFindRect(
            HearthCardsManifestFile manifest, CardVisualSlot slot, CardType type, out Rect rect)
        {
            rect = default;

            if (manifest?.entries == null)
            {
                return false;
            }

            bool found = false;
            bool foundTyped = false;

            for (int index = 0; index < manifest.entries.Length; index++)
            {
                HearthCardsEntry entry = manifest.entries[index];

                if (entry == null || !entry.TryReadSlot(out CardVisualSlot entrySlot) || entrySlot != slot)
                {
                    continue;
                }

                if (!entry.sourceRect.Exists)
                {
                    continue;
                }

                bool hasType = entry.TryReadType(out CardType entryType);

                if (hasType && entryType != type)
                {
                    continue;
                }

                // A rectangle written for this type beats one written for any
                // card, and nothing else can displace it.
                if (hasType)
                {
                    rect = entry.sourceRect.ToRect();
                    return true;
                }

                if (!foundTyped)
                {
                    rect = entry.sourceRect.ToRect();
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Every entry, in the order the manifest lists them.</summary>
        public static IEnumerable<HearthCardsEntry> Entries(HearthCardsManifestFile manifest)
        {
            if (manifest?.entries == null)
            {
                yield break;
            }

            for (int index = 0; index < manifest.entries.Length; index++)
            {
                if (manifest.entries[index] != null)
                {
                    yield return manifest.entries[index];
                }
            }
        }
    }
}
