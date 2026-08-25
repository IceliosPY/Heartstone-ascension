using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoH.Core.Cards;
using CoH.Core.Effects;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// A fingerprint of what the cards actually do.
    ///
    /// The same seed and the same commands do not reproduce a match if a card
    /// has been re-tuned in between: a Test Soldier that used to cost two and
    /// now costs three turns a valid replay into a stream of rejections that
    /// look like an engine bug. Recording this alongside the seed is what turns
    /// that confusion into one clear message.
    ///
    /// Only what the rules can act on goes in. Artwork, frames and colours
    /// never reach the engine at all, so re-drawing a card must not invalidate
    /// a replay of a match it was in; the name and the rules text are written
    /// for a person and nothing parses them, so rewording a card must not
    /// either. What is left is what could change how a match plays out.
    ///
    /// Cards are sorted by id before hashing, so a catalog built in a different
    /// order fingerprints the same.
    /// </summary>
    public static class CatalogFingerprint
    {
        /// <summary>The canonical description every catalog hash is taken of.</summary>
        public static string Describe(ICardCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            List<CardDefinition> ordered = new List<CardDefinition>(catalog.Cards);

            ordered.Sort((left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));

            StringBuilder text = new StringBuilder();
            text.Append("catalog v1 count=").Append(ordered.Count).Append('\n');

            for (int index = 0; index < ordered.Count; index++)
            {
                CardDefinition card = ordered[index];

                text.Append(card.Id.Value)
                    .Append('|').Append(card.Type)
                    .Append('|').Append(Number(card.ManaCost))
                    .Append('|').Append(Number(card.Attack))
                    .Append('|').Append(Number(card.Health))
                    .Append('|').Append(card.Collectible ? '1' : '0')
                    .Append('|').Append(card.Class)
                    .Append('|').Append(card.Rarity)
                    .Append('|').Append(card.Tribe);

                // What a card does is the most gameplay-relevant thing
                // about it. Re-tuning a battlecry from two damage to
                // three has to invalidate a replay of a match it was in,
                // exactly as changing its cost does.
                text.Append('|').Append(card.Effects.Count);

                for (int effect = 0; effect < card.Effects.Count; effect++)
                {
                    EffectDefinition described = card.Effects[effect];

                    text.Append('|').Append(described.Trigger)
                        .Append(':').Append(described.Selector.Kind)
                        .Append(':').Append(described.Selector.Filter)
                        .Append(':').Append(described.Action.Kind)
                        .Append(':').Append(Number(described.Action.Amount))
                        .Append(':').Append(Number(described.Action.AttackDelta))
                        .Append(':').Append(Number(described.Action.HealthDelta))
                        .Append(':').Append(described.Action.SummonCardId.Value)
                        .Append(':').Append(Number(described.Action.SummonCount))
                        .Append(':').Append(described.Action.Placement);
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        public static string Of(ICardCatalog catalog) => StableHash.Hex(Describe(catalog));

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
