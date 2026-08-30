using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Which authored object a property belongs to.
    ///
    /// A layer's rectangle is that layer's own business; the outline its text
    /// carries belongs to the style, and every layer set in that style shares
    /// it. Editing one is a change to one card's worth of geometry and editing
    /// the other is a change to every card of a kind, so which is which has to
    /// be part of a property's identity rather than something a tool infers.
    /// </summary>
    public enum CardVisualPropertyOwner
    {
        Layer = 0,
        Style = 1
    }

    /// <summary>
    /// How far a property may be authored, and by whom.
    ///
    /// This exists because "public field" and "safely editable" turned out to
    /// be very different sets. The schema used to assume every public field of
    /// an authored type was overridable per card unless somebody remembered to
    /// say otherwise, and the editor duly offered controls for fields that were
    /// read before a card's own adjustments are applied - or, in one case, read
    /// nowhere at all. Changing them appeared to work and did nothing, which is
    /// worse for an authoring tool than not offering them.
    ///
    /// So authorability is now stated rather than assumed, and a contract test
    /// composes a card with each <see cref="PerCard"/> property overridden and
    /// fails if the composed result does not actually change.
    /// </summary>
    public enum CardVisualAuthorability
    {
        /// <summary>
        /// Authored on the profile, and one card may differ. Reaches the
        /// composed plan through <see cref="CardVisualInheritance.WithOverrides{T}"/>.
        /// </summary>
        PerCard = 0,

        /// <summary>
        /// Authored on the profile only.
        ///
        /// Not a limitation of the plumbing but a decision: a card that chose
        /// its own font role would set its title in the rules face, which the
        /// project has settled as an invariant rather than a preference.
        /// </summary>
        ProfileOnly = 1,

        /// <summary>
        /// Decided before a card's adjustments are looked at, so overriding it
        /// could not take effect even if the plumbing allowed it.
        ///
        /// Which layers a card draws, what each one is for, and which style it
        /// is set in are all settled while the composer is still choosing
        /// layers. Shown read-only, because seeing them is useful and editing
        /// them here would be a lie.
        /// </summary>
        Structural = 2,

        /// <summary>
        /// Authored, serialised, and read by nothing.
        ///
        /// Kept visible and clearly marked rather than quietly hidden, so that
        /// the field's presence in the asset has an explanation instead of
        /// looking like something that ought to work.
        /// </summary>
        Unsupported = 3,

        /// <summary>
        /// What other data points at this thing by. Never editable here.
        ///
        /// Distinct from <see cref="Structural"/>, which is ordinary authoring
        /// a card simply cannot differ on: changing one of these does not
        /// change a card at all, it silently breaks whatever named it. Shown,
        /// because knowing a layer's id is exactly what somebody debugging an
        /// orphaned adjustment needs, and read-only, because a text field in a
        /// polishing tool is not the place to renumber authored data.
        /// </summary>
        Identity = 4
    }

    /// <summary>
    /// One thing about a card's appearance that somebody can change.
    ///
    /// Discovered rather than declared. The authored types already carry
    /// everything a tool needs in order to draw a sensible control - the field's
    /// type, its tooltip, the group it was written under, the range it is
    /// clamped to - and writing that down a second time in an editor is how a
    /// tool starts needing a change every time the data gains a field. So the
    /// schema is read off the data by reflection, once, and cached.
    ///
    /// Discovery is reflective; *identity* is not. What a saved override names
    /// is <see cref="Id"/>, which is stated on the field and outlives renaming
    /// it - see <see cref="CardVisualPropertyAttribute"/>.
    /// </summary>
    public sealed class CardVisualProperty
    {
        internal CardVisualProperty(CardVisualPropertyOwner owner, FieldInfo field, string group)
        {
            Owner = owner;
            Field = field;
            Group = group;

            DisplayName = Prettify(field.Name);
            Type = field.FieldType;
            FieldName = field.Name;

            TooltipAttribute tooltip = field.GetCustomAttribute<TooltipAttribute>();
            Tooltip = tooltip == null ? string.Empty : tooltip.tooltip;

            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();

            HasRange = range != null;
            Lowest = range == null ? 0f : range.min;
            Highest = range == null ? 0f : range.max;

            CardVisualPropertyAttribute marked = field.GetCustomAttribute<CardVisualPropertyAttribute>();

            string prefix = Prefix(owner);

            Id = prefix + (marked != null && !string.IsNullOrEmpty(marked.Id) ? marked.Id : field.Name);

            Authorability = marked?.Authorability ?? CardVisualAuthorability.PerCard;
            Note = marked?.Note ?? string.Empty;

            string[] former = marked?.FormerIds;
            string[] aliases = new string[former?.Length ?? 0];

            for (int index = 0; index < aliases.Length; index++)
            {
                aliases[index] = prefix + former[index];
            }

            FormerIds = aliases;
        }

        /// <summary>The prefix an owner's ids carry, so a layer's width and a style's cannot collide.</summary>
        public static string Prefix(CardVisualPropertyOwner owner) =>
            owner == CardVisualPropertyOwner.Layer ? "layer." : "style.";

        /// <summary>
        /// What a saved override names this property by. Permanent.
        ///
        /// Defaults to the field's name, which is what every id in the project
        /// currently is, and is stated explicitly on the field the moment the
        /// two need to diverge. Renaming the C# field then costs an <see
        /// cref="CardVisualPropertyAttribute.Id"/> naming the old id, and
        /// authored data keeps resolving.
        /// </summary>
        public string Id { get; }

        /// <summary>Ids this property used to answer to. Read, never written.</summary>
        public IReadOnlyList<string> FormerIds { get; }

        /// <summary>The C# field behind it. Diagnostics only - never identity.</summary>
        public string FieldName { get; }

        public CardVisualPropertyOwner Owner { get; }

        public string DisplayName { get; }

        /// <summary>The heading the field was written under, or empty.</summary>
        public string Group { get; }

        public Type Type { get; }

        public string Tooltip { get; }

        public bool HasRange { get; }

        public float Lowest { get; }

        public float Highest { get; }

        public CardVisualAuthorability Authorability { get; }

        /// <summary>Why it is not freely editable, when it is not. For the editor to show.</summary>
        public string Note { get; }

        /// <summary>Whether one card may differ from its type here.</summary>
        public bool SupportsCardOverride => Authorability == CardVisualAuthorability.PerCard;

        /// <summary>
        /// Whether the recipe may set it at all.
        ///
        /// Structural properties are included: which slot a layer draws and
        /// when it applies is ordinary profile authoring. What they are not is
        /// something one card can differ on, which is a different question and
        /// the one <see cref="SupportsCardOverride"/> answers.
        /// </summary>
        public bool SupportsProfileEdit =>
            Authorability == CardVisualAuthorability.PerCard ||
            Authorability == CardVisualAuthorability.ProfileOnly ||
            Authorability == CardVisualAuthorability.Structural;

        internal FieldInfo Field { get; }

        public object Read(object from) => from == null ? null : Field.GetValue(from);

        public void Write(object to, object value)
        {
            if (to != null && value != null && Field.FieldType.IsInstanceOfType(value))
            {
                Field.SetValue(to, value);
            }
        }

        /// <summary>fontSizeMin becomes "Font Size Min".</summary>
        private static string Prettify(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            System.Text.StringBuilder text = new System.Text.StringBuilder();

            text.Append(char.ToUpperInvariant(name[0]));

            for (int index = 1; index < name.Length; index++)
            {
                if (char.IsUpper(name[index]) && !char.IsUpper(name[index - 1]))
                {
                    text.Append(' ');
                }

                text.Append(name[index]);
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// States how far a field may be authored, and what a saved override calls
    /// it.
    ///
    /// Both halves exist for the same reason: a serialised override has to keep
    /// meaning what it meant, and a C# field name is a thing a developer may
    /// reasonably rename. Without an id stated here, renaming a field silently
    /// orphans every override that named it - the data is still in the asset,
    /// still loaded, and no longer reaches anything.
    ///
    /// Only put it on fields that need it. A field with no attribute is a
    /// per-card property whose id is its own name, which is right for almost
    /// all of them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CardVisualPropertyAttribute : Attribute
    {
        public CardVisualPropertyAttribute(
            CardVisualAuthorability authorability = CardVisualAuthorability.PerCard) =>
            Authorability = authorability;

        public CardVisualAuthorability Authorability { get; }

        /// <summary>
        /// The permanent id, without the owner's prefix. Defaults to the field
        /// name. Once authored data exists, this may never change.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Ids this property used to be saved under, without the prefix.
        ///
        /// Add the old id here when renaming, and authored overrides keep
        /// resolving. They are read, never written: anything saved afterwards
        /// carries the current <see cref="Id"/>.
        /// </summary>
        public string[] FormerIds { get; set; }

        /// <summary>Why this is not freely editable. Shown in the editor.</summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// Everything about a card's appearance that can be authored, read off the
    /// authored types themselves.
    ///
    /// Cached, because reflection is not free and the answer never changes
    /// within a session: the schema is a fact about the code, not about any
    /// particular card.
    /// </summary>
    public static class CardVisualSchema
    {
        private static CardVisualProperty[] _layer;
        private static CardVisualProperty[] _style;
        private static Dictionary<string, CardVisualProperty> _byId;
        private static List<string> _problems;

        /// <summary>What a layer carries.</summary>
        public static IReadOnlyList<CardVisualProperty> LayerProperties =>
            _layer ??= Discover(typeof(CardVisualLayerDefinition), CardVisualPropertyOwner.Layer);

        /// <summary>What a text style carries.</summary>
        public static IReadOnlyList<CardVisualProperty> StyleProperties =>
            _style ??= Discover(typeof(CardTextStyleDefinition), CardVisualPropertyOwner.Style);

        /// <summary>
        /// Everything wrong with the schema itself: two properties claiming one
        /// id, or an alias that collides with a live id.
        ///
        /// Reported rather than thrown, because a static constructor that
        /// throws takes the whole editor down and tells nobody which field did
        /// it. The validator surfaces these alongside the data's own problems.
        /// </summary>
        public static IReadOnlyList<string> Problems
        {
            get
            {
                Index();
                return _problems;
            }
        }

        /// <summary>
        /// One property by its id, including ids it used to be saved under.
        ///
        /// Null for an id nothing answers to - which is a real answer, and the
        /// validator reports it rather than letting the override quietly do
        /// nothing.
        /// </summary>
        public static CardVisualProperty Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Index();

            return _byId.TryGetValue(id, out CardVisualProperty found) ? found : null;
        }

        /// <summary>Whether an id is one this property used to be saved under.</summary>
        public static bool IsFormerId(string id)
        {
            CardVisualProperty found = Find(id);

            return found != null && !string.Equals(found.Id, id, StringComparison.Ordinal);
        }

        /// <summary>The owner's properties, in the order they were written.</summary>
        public static IReadOnlyList<CardVisualProperty> For(CardVisualPropertyOwner owner) =>
            owner == CardVisualPropertyOwner.Layer ? LayerProperties : StyleProperties;

        private static void Index()
        {
            if (_byId != null)
            {
                return;
            }

            _byId = new Dictionary<string, CardVisualProperty>(StringComparer.Ordinal);
            _problems = new List<string>();

            foreach (CardVisualProperty property in LayerProperties)
            {
                Claim(property.Id, property, "id");
            }

            foreach (CardVisualProperty property in StyleProperties)
            {
                Claim(property.Id, property, "id");
            }

            // Aliases second, so a live id always wins a collision with one.
            foreach (CardVisualProperty property in LayerProperties)
            {
                foreach (string alias in property.FormerIds)
                {
                    Claim(alias, property, "former id");
                }
            }

            foreach (CardVisualProperty property in StyleProperties)
            {
                foreach (string alias in property.FormerIds)
                {
                    Claim(alias, property, "former id");
                }
            }
        }

        private static void Claim(string id, CardVisualProperty property, string what)
        {
            if (string.IsNullOrEmpty(id))
            {
                _problems.Add(property.FieldName + " declares an empty " + what + ".");
                return;
            }

            if (_byId.TryGetValue(id, out CardVisualProperty already))
            {
                if (!ReferenceEquals(already, property))
                {
                    _problems.Add(
                        "'" + id + "' is claimed as a " + what + " by both " + already.FieldName +
                        " and " + property.FieldName + ". An override naming it would reach " +
                        "whichever was registered first.");
                }

                return;
            }

            _byId[id] = property;
        }

        private static CardVisualProperty[] Discover(Type type, CardVisualPropertyOwner owner)
        {
            List<CardVisualProperty> found = new List<CardVisualProperty>();
            string group = string.Empty;

            // Declaration order, which is the order somebody chose to write them
            // in and therefore the order they make sense read in. Reflection does
            // not promise it, but every runtime we ship on gives it, and the only
            // cost of being wrong is a panel in a surprising order.
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsNotSerialized)
                {
                    continue;
                }

                HeaderAttribute heading = field.GetCustomAttribute<HeaderAttribute>();

                if (heading != null)
                {
                    group = heading.header;
                }

                if (!IsEditable(field.FieldType))
                {
                    continue;
                }

                found.Add(new CardVisualProperty(owner, field, group));
            }

            return found.ToArray();
        }

        /// <summary>
        /// Whether the editor has any idea how to show this.
        ///
        /// Arrays and the condition list are deliberately left out: they are
        /// authored, but not as a single control, and a property panel that
        /// tried would produce something worse than nothing. They are shown
        /// separately.
        /// </summary>
        public static bool IsEditable(Type type) =>
            type == typeof(float) ||
            type == typeof(int) ||
            type == typeof(bool) ||
            type == typeof(string) ||
            type == typeof(Color) ||
            type == typeof(Vector2) ||
            type == typeof(Vector3) ||
            type == typeof(Rect) ||
            type.IsEnum;

        /// <summary>A value as text, for the places that have to store one.</summary>
        public static string Print(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;

                case float number:
                    return number.ToString("R", CultureInfo.InvariantCulture);

                case Vector2 vector:
                    return vector.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                           vector.y.ToString("R", CultureInfo.InvariantCulture);

                case Color colour:
                    return colour.r.ToString("R", CultureInfo.InvariantCulture) + "," +
                           colour.g.ToString("R", CultureInfo.InvariantCulture) + "," +
                           colour.b.ToString("R", CultureInfo.InvariantCulture) + "," +
                           colour.a.ToString("R", CultureInfo.InvariantCulture);

                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
