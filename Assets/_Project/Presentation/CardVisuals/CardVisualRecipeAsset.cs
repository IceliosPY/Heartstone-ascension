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

    /// <summary>How a picture fills the rectangle it is drawn into.</summary>
    public enum CardVisualFill
    {
        /// <summary>
        /// Stretched to the rectangle exactly. Right for a component drawn at
        /// its own size, which every downloaded one is.
        /// </summary>
        Stretch = 0,

        /// <summary>
        /// Scaled up until it covers the rectangle, keeping its proportions,
        /// and allowed to overflow. Right for artwork, which is a painting of
        /// unknown shape that has to fill a window without being squashed —
        /// and which a mask then crops.
        /// </summary>
        Cover = 1,

        /// <summary>Scaled down until it fits inside, keeping its proportions.</summary>
        Contain = 2
    }

    /// <summary>Where a label sits inside its rectangle.</summary>
    public enum CardVisualAlignment
    {
        Center = 0,
        Top = 1,
        Bottom = 2,
        Left = 3,
        Right = 4
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
        [Tooltip(
            "Permanent identity. Saved adjustments name this, never the label below, so a " +
            "layer can be renamed and reordered without orphaning them. Never change it once " +
            "cards have been polished.")]
        [CardVisualProperty(CardVisualAuthorability.Identity,
            Note = "Permanent identity. Changing it orphans every adjustment that names it.")]
        public string id = string.Empty;

        [Tooltip("A label for the inspector. Free to change; nothing is saved against it.")]
        [CardVisualProperty(CardVisualAuthorability.ProfileOnly,
            Note = "A display label, free to change: adjustments are saved against the id.")]
        public string name = "Layer";

        [Tooltip("What picture goes here. None for a layer that only prints text.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Chooses the picture before a card's own adjustments are read.")]
        public CardVisualSlot slot = CardVisualSlot.None;

        [Tooltip("What this layer prints. None for a layer that is only a picture.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Decides whether this layer is a picture or a label, before anything else.")]
        public CardVisualTextSlot text = CardVisualTextSlot.None;

        [Tooltip("Which side of the card this layer belongs to.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Selects whether the layer applies at all, before adjustments are read.")]
        public CardVisualFace face = CardVisualFace.FaceUp;

        [Tooltip("Higher draws in front. Leave gaps, so a layer can be inserted between two later.")]
        public int sortingOrder;

        [Tooltip("Left edge on the 800 x 1100 card canvas.")]
        public float x;

        [Tooltip("Top edge on the 800 x 1100 card canvas.")]
        public float y;

        // A real, live former id, kept for exactly one reason: proving the
        // alias mechanism against the real schema and the real composer
        // rather than against a type built only for a test. Nothing in this
        // project has ever actually been called "boxWidth" - the alias exists
        // so CardVisualContractTests can store an override under it and watch
        // the value reach a composed CardVisualPlan, which a test double
        // cannot demonstrate because WithOverrides iterates the real schema.
        [CardVisualProperty(FormerIds = new[] { "boxWidth" })]
        public float width = 100f;
        public float height = 100f;

        [Tooltip("Degrees, clockwise.")]
        public float rotation;

        [Tooltip("How the picture fills its rectangle.")]
        public CardVisualFill fill = CardVisualFill.Stretch;

        [Tooltip(
            "Clip this layer to the shape of the picture in another slot. None for no clipping. " +
            "Used for artwork, which is a rectangle that has to sit inside a frame's window.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Resolved from the catalog before a card's own adjustments are read.")]
        public CardVisualSlot maskSlot = CardVisualSlot.None;

        [Header("Text")]
        [Tooltip(
            "Which of the recipe's text styles this label is set in. Empty falls back to a " +
            "plain style chosen from the text slot, which is what every layer authored before " +
            "styles existed still gets.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Selects the style before adjustments are applied; a card adjusts the style " +
                   "its layer already uses rather than pointing at another one.")]
        public string textStyle = string.Empty;

        [Tooltip("Largest point size. Text shrinks from here to fit its rectangle.")]
        public float fontSize = 3f;

        [Tooltip("Smallest it may shrink to before it is allowed to overflow instead.")]
        public float fontSizeMin = 0.6f;

        public bool bold;

        public CardVisualAlignment alignment = CardVisualAlignment.Center;

        [Tooltip("Whether a long line wraps. Off for a number, on for a sentence.")]
        public bool wrap = true;

        public Color tint = Color.white;

        [Tooltip(
            "A layer whose picture is missing is normally skipped in silence, because most " +
            "layers are optional. Mark the ones that are not, and the composer will report " +
            "the gap instead of quietly drawing a card with a hole in it.")]
        [CardVisualProperty(CardVisualAuthorability.Structural,
            Note = "Read while resolving pictures, before a card's own adjustments exist.")]
        public bool required;

        [Tooltip("Every one of these must hold for the layer to appear. Empty means always.")]
        public CardVisualCondition[] conditions = Array.Empty<CardVisualCondition>();

        /// <summary>
        /// What adjustments name this layer by.
        ///
        /// Falls back to the label for data authored before ids existed, so
        /// nothing breaks the moment the field appears and before anything has
        /// been migrated. The validator reports every layer still relying on
        /// that, because it is the state in which a rename still loses data.
        /// </summary>
        public string LayerId => string.IsNullOrEmpty(id) ? name : id;

        /// <summary>Whether this layer has a real id rather than falling back to its label.</summary>
        public bool HasStableId => !string.IsNullOrEmpty(id);

        public bool IsText => text != CardVisualTextSlot.None;

        public bool AppliesTo(in CardVisualDescriptor card) =>
            ShowsOn(card.IsFaceDown) && CardVisualCondition.AllMatch(conditions, card);

        /// <summary>Why this layer applies, in words. For the reports.</summary>
        public string Describe()
        {
            if (conditions == null || conditions.Length == 0)
            {
                return face == CardVisualFace.FaceUp ? "always" : face.ToString();
            }

            string[] parts = new string[conditions.Length];

            for (int index = 0; index < conditions.Length; index++)
            {
                parts[index] = conditions[index].Describe();
            }

            return string.Join(" and ", parts);
        }

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

        [Tooltip(
            "How each kind of writing on a card looks. Layers select one by name, so a minion " +
            "title and a spell title are two rows here rather than two code paths.")]
        [SerializeField] private List<CardTextStyleDefinition> textStyles = new List<CardTextStyleDefinition>();

        public CardVisualStyle Style => style;

        public IReadOnlyList<CardVisualLayerDefinition> Layers => layers;

        public IReadOnlyList<CardTextStyleDefinition> TextStyles => textStyles;

        /// <summary>
        /// The style a layer asks for, or the fallback for its text slot.
        ///
        /// Never null and never an exception. A recipe that names a style it
        /// does not define is an authoring mistake the validator reports by
        /// name; the card still draws in the meantime, in a plain style, rather
        /// than losing its title because of a typo.
        /// </summary>
        public CardTextStyle ResolveTextStyle(CardVisualLayerDefinition layer)
        {
            if (layer == null)
            {
                return CardTextStyle.For(CardVisualTextSlot.None);
            }

            if (string.IsNullOrEmpty(layer.textStyle))
            {
                return CardTextStyle.For(layer.text);
            }

            CardTextStyleDefinition found = FindTextStyle(layer.textStyle);

            return found == null
                ? CardTextStyle.For(layer.text)
                : CardTextStyle.From(found, layer.text);
        }

        /// <summary>
        /// The style definition a layer is set in, or null.
        ///
        /// The definition rather than the resolved copy, because a card's own
        /// adjustments are authored against the schema of the definition and
        /// have to be applied before it is resolved.
        /// </summary>
        public CardTextStyleDefinition TextStyleFor(CardVisualLayerDefinition layer) =>
            layer == null ? null : FindTextStyle(layer.textStyle);

        /// <summary>The named style, or null. Editor tooling and the validator.</summary>
        public CardTextStyleDefinition FindTextStyle(string wanted)
        {
            if (string.IsNullOrEmpty(wanted))
            {
                return null;
            }

            for (int index = 0; index < textStyles.Count; index++)
            {
                CardTextStyleDefinition candidate = textStyles[index];

                if (candidate != null &&
                    string.Equals(candidate.name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

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

            HashSet<string> named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < textStyles.Count; index++)
            {
                CardTextStyleDefinition definition = textStyles[index];

                if (definition == null)
                {
                    problems.Add(name + ": text style " + index + " is empty.");
                    continue;
                }

                definition.Validate(name, problems);

                if (!string.IsNullOrWhiteSpace(definition.name) && !named.Add(definition.name))
                {
                    problems.Add(name + ": two text styles are both called '" + definition.name +
                        "', so which one a layer gets depends on list order.");
                }
            }

            Dictionary<int, List<CardVisualLayerDefinition>> atDepth =
                new Dictionary<int, List<CardVisualLayerDefinition>>();

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

                if (layer.IsText && layer.fontSizeMin > layer.fontSize)
                {
                    problems.Add(
                        where + " may not shrink below " + layer.fontSizeMin +
                        " but starts at " + layer.fontSize + ", so it can only grow.");
                }

                if (!string.IsNullOrEmpty(layer.textStyle))
                {
                    if (!layer.IsText)
                    {
                        problems.Add(where + " is a picture that names a text style.");
                    }
                    else if (FindTextStyle(layer.textStyle) == null)
                    {
                        problems.Add(where + " asks for the text style '" + layer.textStyle +
                            "', which this recipe does not define.");
                    }
                }

                if (layer.maskSlot != CardVisualSlot.None && layer.IsText)
                {
                    problems.Add(where + " is a label with a mask, which does nothing.");
                }

                if (layer.maskSlot == layer.slot && layer.slot != CardVisualSlot.None)
                {
                    problems.Add(where + " is masked by itself.");
                }

                // Two layers at one depth only matter if a card could ever
                // have both. A slot whose picture and rectangle differ by card
                // type is written as several layers, and a card is one type, so
                // they never meet and their order never comes up.
                //
                // Compared against everything already at that depth rather than
                // against the first one: three layers where only two of them
                // exclude each other is still an ambiguity, and checking only
                // the first would miss exactly that.
                if (!atDepth.TryGetValue(layer.sortingOrder, out List<CardVisualLayerDefinition> neighbours))
                {
                    neighbours = new List<CardVisualLayerDefinition>();
                    atDepth[layer.sortingOrder] = neighbours;
                }

                for (int other = 0; other < neighbours.Count; other++)
                {
                    if (!CardVisualCondition.MutuallyExclusive(layer.conditions, neighbours[other].conditions))
                    {
                        problems.Add(
                            where + " shares sorting order " + layer.sortingOrder + " with '" +
                            neighbours[other].name + "', and a card could have both, so which draws " +
                            "in front depends on list order.");
                    }
                }

                neighbours.Add(layer);
            }
        }

#if UNITY_EDITOR
        /// <summary>Replaces the whole recipe. Editor tooling only.</summary>
        internal void Author(CardVisualStyle newStyle, IEnumerable<CardVisualLayerDefinition> newLayers)
        {
            style = newStyle;
            layers = new List<CardVisualLayerDefinition>(newLayers);
        }

        /// <summary>
        /// Replaces the text styles, leaving every layer alone.
        ///
        /// Separate from <see cref="Author"/> on purpose. Rebuilding a recipe
        /// throws away the rectangles somebody spent an evening nudging into
        /// place; adding the styles the layers refer to must not, so the two are
        /// not the same operation and cannot be confused for one another.
        /// </summary>
        internal void AuthorTextStyles(IEnumerable<CardTextStyleDefinition> newStyles)
        {
            textStyles = new List<CardTextStyleDefinition>(newStyles);
        }

        /// <summary>
        /// Sets how tall one label's box is, keeping it centred where it was.
        ///
        /// Height alone, and re-centred rather than moved: how tall a title's
        /// box is decides how big the title is, and is therefore a typographic
        /// number rather than a placement one. Where the banner sits across the
        /// card stays whatever it was tuned to.
        /// </summary>
        internal void SetTextHeight(string layerName, float height)
        {
            for (int index = 0; index < layers.Count; index++)
            {
                CardVisualLayerDefinition layer = layers[index];

                if (layer == null ||
                    !string.Equals(layer.name, layerName, StringComparison.Ordinal))
                {
                    continue;
                }

                float middle = layer.y + layer.height * 0.5f;

                layer.height = height;
                layer.y = middle - height * 0.5f;
            }
        }

        /// <summary>Points one layer at a style, leaving its geometry alone.</summary>
        internal void AssignTextStyle(string layerName, string styleName)
        {
            for (int index = 0; index < layers.Count; index++)
            {
                if (layers[index] != null &&
                    string.Equals(layers[index].name, layerName, StringComparison.Ordinal))
                {
                    layers[index].textStyle = styleName;
                }
            }
        }
#endif
    }
}
