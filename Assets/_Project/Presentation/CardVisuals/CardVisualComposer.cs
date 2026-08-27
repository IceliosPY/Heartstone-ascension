using System.Globalization;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Turns a description of a card into the stack of pictures and labels that
    /// draws it.
    ///
    /// This is the only road from "a two mana neutral minion called Test
    /// Soldier" to a finished card, and everything that shows a card walks it:
    /// the hand, and the preview tool in the editor. There is no second
    /// renderer, so a preview is not a guess about what the game will draw. It
    /// is what the game will draw.
    ///
    /// The whole of it is three steps, and none of them knows what a card is:
    ///
    ///   the recipe says which layers a card can have, and when;
    ///   the catalog says which picture each layer gets;
    ///   this puts them in order.
    ///
    /// It creates nothing, touches no GameObject and reads no game state, which
    /// is why it can be tested without a scene and why adding a card is adding
    /// data. There is no card id anywhere below, and there is no place to put
    /// one: the composer is never told which card it is drawing, only what that
    /// card is like.
    /// </summary>
    public static class CardVisualComposer
    {
        /// <summary>
        /// Composes a card into a plan, reusing the plan given to it.
        ///
        /// A missing picture is not an error and not an exception. Most layers
        /// are optional — a spell has no health gem, a card with no tribe has no
        /// plaque, and a frame that already draws its own name banner leaves
        /// that slot empty on purpose. Layers the recipe marks required are the
        /// exception, and those are collected into the plan rather than thrown,
        /// so an incomplete card can still be drawn and still be reported.
        /// </summary>
        public static void Compose(
            in CardVisualDescriptor card,
            CardVisualRecipeAsset recipe,
            CardVisualCatalogAsset catalog,
            CardVisualPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            plan.Clear();

            if (recipe == null)
            {
                return;
            }

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || !layer.AppliesTo(card))
                {
                    continue;
                }

                // A mask is a shape to clip by, never a picture to show. A
                // recipe that listed one as a layer would paint it over the
                // card, so the slot is refused here rather than trusted.
                if (layer.slot == CardVisualSlot.ArtworkMask)
                {
                    continue;
                }

                if (layer.IsText)
                {
                    AddText(card, layer, recipe, plan);
                    continue;
                }

                AddSprite(card, layer, catalog, plan);
            }

            plan.SortByDepth();
        }

        private static void AddSprite(
            in CardVisualDescriptor card,
            CardVisualLayerDefinition layer,
            CardVisualCatalogAsset catalog,
            CardVisualPlan plan)
        {
            // Artwork belongs to the card rather than to a kind of card, which
            // is the one reason a slot is ever answered from somewhere other
            // than the catalog. It is also what lets one frame serve any number
            // of paintings without a second entry anywhere.
            Sprite sprite = layer.slot == CardVisualSlot.Artwork
                ? card.Artwork
                : catalog != null ? catalog.Resolve(layer.slot, card).Sprite : null;

            if (sprite == null)
            {
                if (layer.required)
                {
                    plan.ReportGap(new CardVisualGap(layer.slot, layer.name, card.ToString()));
                }

                return;
            }

            // The shape to clip to is looked up exactly like the picture is,
            // so a minion's oval window and a spell's rectangular one are two
            // rows in the catalog rather than two branches anywhere.
            Sprite mask = layer.maskSlot == CardVisualSlot.None || catalog == null
                ? null
                : catalog.Resolve(layer.maskSlot, card).Sprite;

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = layer.slot,
                TextSlot = CardVisualTextSlot.None,
                Sprite = sprite,
                Mask = mask,
                Text = null,
                SortingOrder = layer.sortingOrder,
                Rect = new Rect(layer.x, layer.y, layer.width, layer.height),
                Rotation = layer.rotation,
                Fill = layer.fill,
                FontSize = 0f,
                FontSizeMin = 0f,
                Bold = false,
                Wrap = false,
                Alignment = CardVisualAlignment.Center,
                Tint = layer.tint,
                LayerName = layer.name,
                Reason = layer.Describe()
            });
        }

        private static void AddText(
            in CardVisualDescriptor card,
            CardVisualLayerDefinition layer,
            CardVisualRecipeAsset recipe,
            CardVisualPlan plan)
        {
            string value = Read(card, layer.text);

            // An empty label is not drawn at all rather than drawn empty, so a
            // card with no rules text has no rules text rather than a blank
            // rectangle sitting in the middle of it.
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            Rect rect = new Rect(layer.x, layer.y, layer.width, layer.height);
            float fontSize = layer.fontSize;

            // Which style this label is set in is a question about the recipe,
            // so it is answered here rather than left for whatever draws the
            // plan to look up.
            CardTextStyle style = recipe.ResolveTextStyle(layer);

            // And then, for the handful of cards that have been polished by
            // hand, what that card wants done differently. The recipe is still
            // what decided everything above; this only nudges it, and a card
            // that asks for nothing is composed exactly as though none of this
            // were here.
            //
            // Note what is *not* available at this point: which card it is.
            // Whatever built the description looked the overrides up by id and
            // handed them over as data, so there is nowhere below to write a
            // special case for one card.
            CardTextOverride polish = card.Overrides?.For(layer.text);

            if (polish != null)
            {
                rect = polish.Placed(rect);
                fontSize = polish.Sized(fontSize);
                style = polish.Styled(style);
            }

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = CardVisualSlot.None,
                TextSlot = layer.text,
                Sprite = null,
                Text = value,
                SortingOrder = layer.sortingOrder,
                Rect = rect,
                Rotation = layer.rotation,
                Fill = CardVisualFill.Stretch,
                FontSize = fontSize,
                FontSizeMin = Mathf.Min(layer.fontSizeMin, fontSize),
                Bold = layer.bold,
                Wrap = layer.wrap,
                Alignment = layer.alignment,
                Tint = layer.tint,
                TextStyle = style,
                LayerName = layer.name,
                Reason = layer.Describe()
            });
        }

        private static string Read(in CardVisualDescriptor card, CardVisualTextSlot slot)
        {
            switch (slot)
            {
                case CardVisualTextSlot.Name:
                    return card.Name;

                case CardVisualTextSlot.RulesText:
                    return card.RulesText;

                case CardVisualTextSlot.ManaCost:
                    return card.ShowsCost
                        ? card.ManaCost.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Attack:
                    return card.ShowsStatistics
                        ? card.Attack.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Health:
                    return card.ShowsStatistics
                        ? card.Health.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Tribe:
                    return card.HasTribe ? card.Tribe.ToString().ToUpperInvariant() : string.Empty;

                default:
                    return string.Empty;
            }
        }
    }
}
