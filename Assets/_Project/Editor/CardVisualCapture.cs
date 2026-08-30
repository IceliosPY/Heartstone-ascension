using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Renders composed cards to image files.
    ///
    /// The same composer, the same painter and the same canvas as the game and
    /// the preview window; only the camera is different. Useful for looking at
    /// a change properly rather than squinting at a card an inch tall in a
    /// screenshot of a board, and for producing a contact sheet of every
    /// variant when the catalog changes.
    /// </summary>
    public static class CardVisualCapture
    {
        private const string OutputFolder = "CardCaptures";

        [MenuItem("Conquest of Hearthstone/Capture Card Variants")]
        public static void CaptureVariants()
        {
            CardVisualFactory factory =
                AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);

            if (factory == null)
            {
                Debug.LogError("No card visual factory. Run Create Missing Card Visual Assets first.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);

            List<string> written = new List<string>();

            Capture(factory, Card(CardType.Minion, Rarity.Free, "Test Soldier", ""), "minion-basic", written);
            Capture(factory, Card(CardType.Minion, Rarity.Common, "Test Scribe",
                "Deathrattle: Draw a card."), "minion-common-text", written);
            Capture(factory, Card(CardType.Minion, Rarity.Legendary, "Test Champion",
                "Battlecry: Deal 2 damage."), "minion-legendary", written);
            Capture(factory, Card(CardType.Spell, Rarity.Rare, "Test Volley",
                "Deal 1 damage to all enemy minions."), "spell-rare", written);
            Capture(factory, Card(CardType.Weapon, Rarity.Epic, "Test Blade", ""), "weapon-epic", written);

            // The two that stress the dynamic layout rather than the pictures:
            // a name longer than the banner was drawn for, and rules text long
            // enough to need several lines inside the parchment.
            Capture(factory, Card(CardType.Minion, Rarity.Rare, "Test Deathrattle Draw",
                "Deathrattle: Draw a card."), "minion-long-name", written);

            Capture(factory, Card(CardType.Minion, Rarity.Epic, "Test Battlecry Damage",
                "Battlecry: Deal 2 damage to a chosen enemy character, then draw a card."),
                "minion-long-text", written);

            // Two digits everywhere, which is where a number last ran off its
            // gem. A ten mana card is ordinary; a thirty health one is a hero.
            Capture(factory, new CardVisualDescriptor(
                CardType.Minion, CardClass.Neutral, Rarity.Legendary, Tribe.None,
                artwork: null, name: "Test Colossus", rulesText: "Battlecry: Gain 10 Armor.",
                manaCost: 10, attack: 12, health: 30,
                showsCost: true, showsStatistics: true), "minion-two-digits", written);

            CardVisualDescriptor back = new CardVisualDescriptor(
                CardType.None, CardClass.Neutral, faceDown: true);
            Capture(factory, back, "card-back", written);

            Debug.Log("Card captures written:\n" + string.Join("\n", written));
        }

        private static CardVisualDescriptor Card(CardType type, Rarity rarity, string name, string rules) =>
            new CardVisualDescriptor(
                type,
                CardClass.Neutral,
                rarity,
                Tribe.None,
                artwork: null,
                name: name,
                rulesText: rules,
                manaCost: 2,
                attack: 2,
                health: 3,
                showsCost: true,
                showsStatistics: type == CardType.Minion || type == CardType.Weapon);

        /// <summary>Composes one card and writes it out, at the given width.</summary>
        public static void Capture(
            CardVisualFactory factory,
            in CardVisualDescriptor card,
            string fileName,
            List<string> written = null,
            int width = 400)
        {
            CardVisualPlan plan = new CardVisualPlan();

            // The artwork a card with none of its own would really be given, so
            // a capture is not misleadingly emptier than the game.
            CardVisualDescriptor described = card.HasArtwork || factory.Library == null
                ? card
                : card.With(factory.Library.ArtworkFor(default));

            factory.Compose(described, plan);

            GameObject stage = new GameObject("Capture") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(5000f, 5000f, 5000f);

            // The real prefab, so a still is drawn by the same painter the game
            // uses, with the same fonts on it. Built by hand this was a card in
            // the fallback face, which looked plausible and was not the card.
            CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject cardObject);
            painter.Apply(plan);

            GameObject eye = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            eye.transform.SetParent(stage.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -3f);

            Camera camera = eye.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CardCanvas.CardHeight * 0.52f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            camera.enabled = false;

            int height = Mathf.RoundToInt(width * (CardCanvas.Height / CardCanvas.Width));

            RenderTexture target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;

            // Twice. In batch mode the first render of a session can land
            // before the shaders it needs are ready, and produces a black
            // frame that looks exactly like a composition bug.
            camera.Render();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            string path = Path.Combine(OutputFolder, fileName + ".png");
            File.WriteAllBytes(path, image.EncodeToPNG());

            written?.Add(path + "   " + (plan.IsComplete ? "complete" : "INCOMPLETE") +
                ", " + plan.Layers.Count + " layers");

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(stage);
        }
    }
}
