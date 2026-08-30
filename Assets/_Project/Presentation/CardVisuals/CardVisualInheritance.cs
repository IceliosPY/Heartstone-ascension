using System.Reflection;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>Where a resolved value came from.</summary>
    public enum CardVisualSource
    {
        /// <summary>Nobody said otherwise, so the value the code was written with.</summary>
        GlobalDefault = 0,

        /// <summary>The recipe, through whichever layer the card's conditions selected.</summary>
        TypeProfile = 1,

        /// <summary>This card, and only this card.</summary>
        CardOverride = 2
    }

    /// <summary>One property's value and the reason it has it.</summary>
    public readonly struct CardVisualResolved
    {
        public CardVisualResolved(object value, CardVisualSource source, string where)
        {
            Value = value;
            Source = source;
            Where = where;
        }

        public object Value { get; }

        public CardVisualSource Source { get; }

        /// <summary>The name of whatever provided it: a layer, a style, a card.</summary>
        public string Where { get; }

        public string Describe()
        {
            switch (Source)
            {
                case CardVisualSource.CardOverride:
                    return "This card";

                case CardVisualSource.TypeProfile:
                    return string.IsNullOrEmpty(Where) ? "Profile" : Where;

                default:
                    return "Default";
            }
        }
    }

    /// <summary>
    /// Works out what a property ends up as, and why.
    ///
    /// Named for the chain rather than for the act, because the catalog already
    /// has a <c>CardVisualResolution</c> and it answers a different question:
    /// which picture a slot found. This one answers which value a property
    /// inherited, and from whom.
    ///
    /// The why matters as much as the what once a roster is large: "the title is
    /// too wide on this card" is a different problem depending on whether the
    /// width came from the card, from the kind of card, or from nobody having
    /// set it at all, and guessing which is how an afternoon disappears.
    /// </summary>
    public static class CardVisualInheritance
    {
        private static CardVisualLayerDefinition _plainLayer;
        private static CardTextStyleDefinition _plainStyle;

        /// <summary>What a property is, before anybody authors anything.</summary>
        public static object Default(CardVisualProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property.Owner == CardVisualPropertyOwner.Layer)
            {
                return property.Read(_plainLayer ??= new CardVisualLayerDefinition());
            }

            return property.Read(_plainStyle ??= new CardTextStyleDefinition());
        }

        /// <summary>
        /// The value a card ends up with, and where it came from.
        /// </summary>
        /// <param name="authored">The layer or style the recipe selected.</param>
        /// <param name="layerName">Which layer's row a card override would carry.</param>
        public static CardVisualResolved Resolve(
            CardVisualProperty property,
            object authored,
            string layerName,
            string profileName,
            CardVisualOverrides overrides)
        {
            if (property == null)
            {
                return new CardVisualResolved(null, CardVisualSource.GlobalDefault, string.Empty);
            }

            if (property.SupportsCardOverride &&
                overrides != null &&
                overrides.TryResolve(layerName, property, out object overridden))
            {
                return new CardVisualResolved(overridden, CardVisualSource.CardOverride, layerName);
            }

            // No layer and no style: nothing about this kind of card was ever
            // authored, so the value really is whatever the field was written
            // with. This is the only way a value is a global default.
            if (authored == null)
            {
                return new CardVisualResolved(
                    Default(property), CardVisualSource.GlobalDefault, string.Empty);
            }

            // Otherwise it came from the profile - whatever it happens to equal.
            //
            // This used to compare the authored value against the field's
            // initialiser and report a match as "Default", which answered the
            // wrong question. A recipe that sets a width to 100 and a recipe
            // that says nothing both end up at 100, and they are not the same
            // fact: the first is a decision somebody made about this kind of
            // card, the second is nobody having decided. Provenance is asked
            // precisely when those two need telling apart - "why is this value
            // what it is" - so answering it by numeric coincidence made it
            // useless exactly where it mattered. In a Unity asset every field of
            // an authored object carries a value, so an authored object is the
            // source of every one of them.
            return new CardVisualResolved(
                property.Read(authored), CardVisualSource.TypeProfile, profileName);
        }

        /// <summary>
        /// A copy of an authored object with one card's adjustments applied.
        ///
        /// A copy, because the original is a serialised asset shared by every
        /// card of its kind, and a card that wanted a wider title would
        /// otherwise widen the title of every card in the game for as long as
        /// the editor stayed open.
        /// </summary>
        public static T WithOverrides<T>(
            T authored, string layerName, CardVisualOverrides overrides) where T : class, new()
        {
            if (authored == null || overrides == null || overrides.IsEmpty)
            {
                return authored;
            }

            CardVisualPropertyOwner owner = authored is CardVisualLayerDefinition
                ? CardVisualPropertyOwner.Layer
                : CardVisualPropertyOwner.Style;

            T copy = null;

            foreach (CardVisualProperty property in CardVisualSchema.For(owner))
            {
                if (!property.SupportsCardOverride ||
                    !overrides.TryResolve(layerName, property, out object value))
                {
                    continue;
                }

                copy ??= Copy(authored);
                property.Write(copy, value);
            }

            return copy ?? authored;
        }

        /// <summary>
        /// A field for field copy.
        ///
        /// Reflective rather than hand written, so that a field added to the
        /// authored types is copied without anybody remembering to come here.
        /// Forgetting would produce a card that silently lost one property when
        /// it gained an override on another, which is close to the worst kind of
        /// bug this system could have.
        /// </summary>
        private static T Copy<T>(T original) where T : class, new()
        {
            T copy = new T();

            foreach (FieldInfo field in typeof(T).GetFields(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.IsNotSerialized)
                {
                    field.SetValue(copy, field.GetValue(original));
                }
            }

            return copy;
        }
    }
}
