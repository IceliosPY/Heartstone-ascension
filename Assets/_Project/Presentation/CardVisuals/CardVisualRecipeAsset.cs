using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Which side of a card a layer belongs to.
    ///
    /// A mode rather than a condition, because face down is not an attribute of
    /// a card the way its class is: it is which side of it you are looking at.
    /// Written as a condition it would have to be repeated, negated, on every
    /// other layer in the recipe, and the first layer somebody forgot it on
    /// would print a mana cost on the back of an opponent's card.
    /// </summary>
    public enum CardVisualFace
    {
        FaceUp = 0,
        FaceDown = 1,
        Always = 2
    }

    /// <summary>
    /// One layer of a card: where a picture or a label goes, and when.
    ///
    /// The rectangle is in card space, which is the 800 by 1100 canvas the
    /// project's proportions were measured on. Authoring in pixels of that
    /// canvas rather than in world units is what lets the same recipe draw a
    /// card in a hand, on a board and blown up for inspection: the numbers
    /// describe the card, and the layout decides how big the card is.
    /// </summary>
    [Serializable]
    public sealed class CardVisualLayerDefinition
    {
        [Tooltip("A name for the inspector. Nothing reads it.")]
        public string name = "Layer";

        [Tooltip("What picture goes here. None for a layer that only prints text.")]
        public CardVisualSlot slot = CardVisualSlot.None;

        [Tooltip("What this layer prints. None for a layer that is only a picture.")]
        public CardVisualTextSlot text = CardVisualTextSlot.None;

        [Tooltip("Which side of the card this layer belongs to.")]
        public CardVisualFace face = CardVisualFace.FaceUp;

        [Tooltip("Higher draws in front. Leave gaps, so a layer can be inserted between two later.")]
        public int sortingOrder;

        [Tooltip("Left edge on the 800 x 1100 card canvas.")]
        public float x;

        [Tooltip("Top edge on the 800 x 1100 card canvas.")]
        public float y;

        public float width = 100f;
        public float height = 100f;

        [Tooltip("Degrees, clockwise.")]
        public float rotation;

        [Tooltip("Largest point size for a text layer. Text always shrinks to fit.")]
        public float fontSize = 3f;

        public bool bold;

        public Color tint = Color.white;

        [Tooltip(
            "A layer whose picture is missing is normally skipped in silence, because most " +
            "layers are optional. Mark the ones that are not, and the composer will report " +
            "the gap instead of quietly drawing a card with a hole in it.")]
        public bool required;

        [Tooltip("Every one of these must hold for the layer to appear. Empty means always.")]
        public CardVisualCondition[] conditions = Array.Empty<CardVisualCondition>();

        public bool IsText => text != CardVisualTextSlot.None;

        public bool AppliesTo(in CardVisualDescriptor card) =>
            ShowsOn(card.IsFaceDown) && CardVisualCondition.AllMatch(conditions, card);

        public bool ShowsOn(bool faceDown) =>
            face == CardVisualFace.Always ||
            (faceDown ? face == CardVisualFace.FaceDown : face == CardVisualFace.FaceUp);
    }

    /// <summary>
    /// A family of composition: the whole stack of layers a card can be built
    /// from, in the order they are drawn.
    ///
    /// There is deliberately one of these for all cards rather than one per
    /// kind. A minion recipe and a spell recipe would share almost every layer
    /// and drift apart the first time somebody fixed one of them; instead every
    /// layer says when it applies, and a spell is a card whose statistics layers
    /// are switched off. Two recipes are still possible — the composer takes one
    /// as an argument — but they are for two genuinely different card *styles*,
    /// not for two card types.
    ///
    /// What the recipe never contains is a picture. It says a frame goes here;
    /// the catalog says which frame.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardVisualRecipe",
        menuName = "Conquest of Hearthstone/Card Visual Recipe",
        order = 30)]
    public sealed class CardVisualRecipeAsset : ScriptableObject
    {
        [Tooltip("Which family of components this recipe composes. Matched against the card's style.")]
        [SerializeField] private CardVisualStyle style = CardVisualStyle.Default;

        [SerializeField] private List<CardVisualLayerDefinition> layers = new List<CardVisualLayerDefinition>();

        public CardVisualStyle Style => style;

        public IReadOnlyList<CardVisualLayerDefinition> Layers => layers;

        /// <summary>
        /// Checks the recipe on its own, without a catalog.
        ///
        /// Two layers at the same sorting order is the interesting one: which
        /// draws in front is then a matter of list order, which nobody looking
        /// at the card can see, and a card whose appearance depends on that is a
        /// card that will change the day somebody reorders the list.
        /// </summary>
        public void Validate(List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            if (style.IsNone)
            {
                problems.Add(name + ": the recipe has no style, so no card will ever select it.");
            }

            HashSet<int> orders = new HashSet<int>();

            for (int index = 0; index < layers.Count; index++)
            {
                CardVisualLayerDefinition layer = layers[index];

                if (layer == null)
                {
                    problems.Add(name + ": layer " + index + " is empty.");
                    continue;
                }

                string where = name + ", layer '" + layer.name + "'";

                if (layer.slot == CardVisualSlot.None && !layer.IsText)
                {
                    problems.Add(where + " draws neither a picture nor any text.");
                }

                if (layer.slot != CardVisualSlot.None && layer.IsText)
                {
                    problems.Add(
                        where + " is both a picture and a label. Split it in two: the picture " +
                        "is looked up in the catalog and the label is not.");
                }

                if (layer.width <= 0f || layer.height <= 0f)
                {
                    problems.Add(where + " has no size.");
                }

                if (layer.required && layer.IsText)
                {
                    problems.Add(where + " is a text layer marked required, which means nothing.");
                }

                if (!orders.Add(layer.sortingOrder))
                {
                    problems.Add(
                        where + " shares sorting order " + layer.sortingOrder +
                        " with another layer, so which one draws in front depends on list order.");
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>Replaces the whole recipe. Editor tooling only.</summary>
        internal void Author(CardVisualStyle newStyle, IEnumerable<CardVisualLayerDefinition> newLayers)
        {
            style = newStyle;
            layers = new List<CardVisualLayerDefinition>(newLayers);
        }
#endif
    }
}
