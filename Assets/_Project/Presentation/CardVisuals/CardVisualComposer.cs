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

            CardVisualLayerDefinition placed =
                CardVisualInheritance.WithOverrides(layer, layer.LayerId, card.Overrides);

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = placed.slot,
                TextSlot = CardVisualTextSlot.None,
                Sprite = sprite,
                Mask = mask,
                Text = null,
                SortingOrder = placed.sortingOrder,
                Rect = new Rect(placed.x, placed.y, placed.width, placed.height),
                Rotation = placed.rotation,
                Fill = placed.fill,
                FontSize = 0f,
                FontSizeMin = 0f,
                Bold = false,
                Wrap = false,
                Alignment = CardVisualAlignment.Center,
                Tint = placed.tint,
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

            // What this one card wants done differently, if anything.
            //
            // Applied to copies of the authored layer and style rather than
            // read field by field, so a card can adjust anything the schema
            // knows about and nothing here has to learn what that is. A card
            // that asks for nothing is composed from the originals, untouched.
            //
            // Note what is not available at this point: which card it is.
            // Whatever built the description looked these up by id and handed
            // them over as data, so there is nowhere below to write a special
            // case for one card.
            CardVisualOverrides polish = card.Overrides;

            CardVisualLayerDefinition placed =
                CardVisualInheritance.WithOverrides(layer, layer.LayerId, polish);

            // Keyed by the layer rather than by the style, on purpose: a card
            // adjusting the outline of its own title must not thicken the
            // outline of every other label set in the same style.
            CardTextStyleDefinition styled =
                CardVisualInheritance.WithOverrides(recipe.TextStyleFor(layer), layer.LayerId, polish);

            Rect rect = new Rect(placed.x, placed.y, placed.width, placed.height);

            CardTextStyle style = styled == null
                ? CardTextStyle.For(placed.text)
                : CardTextStyle.From(styled, placed.text);

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = CardVisualSlot.None,
                TextSlot = layer.text,
                Sprite = null,
                Text = value,
                SortingOrder = placed.sortingOrder,
                Rect = rect,
                Rotation = placed.rotation,
                Fill = CardVisualFill.Stretch,
                FontSize = placed.fontSize,
                FontSizeMin = Mathf.Min(placed.fontSizeMin, placed.fontSize),
                Bold = placed.bold,
                Wrap = placed.wrap,
                Alignment = placed.alignment,
                Tint = placed.tint,
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
