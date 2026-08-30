using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>What shape a stored value has.</summary>
    public enum CardVisualValueKind
    {
        Number = 0,
        Vector = 1,
        Colour = 2,
        Text = 3
    }

    /// <summary>
    /// One authored value, whatever its type.
    ///
    /// Four fields and a tag rather than anything polymorphic, because Unity
    /// serialises this and a serialised polymorphic value is a custom drawer, a
    /// migration hazard and a class of bug nobody needs. Numbers, enumerations
    /// and yes-or-no all live in the float; points, rectangles and curve handles
    /// live in the vector; colours and text have their own.
    ///
    /// Adding a genuinely new category of value means one more field here and
    /// one more case below. Adding a new property of a category that already
    /// exists means nothing at all.
    /// </summary>
    [Serializable]
    public struct CardVisualValue
    {
        public CardVisualValueKind kind;
        public float number;
        public Vector4 vector;
        public Color colour;
        public string text;

        /// <summary>Captures a value of any type the schema admits.</summary>
        public static CardVisualValue Of(object value)
        {
            switch (value)
            {
                case float number:
                    return new CardVisualValue { kind = CardVisualValueKind.Number, number = number };

                case int number:
                    return new CardVisualValue { kind = CardVisualValueKind.Number, number = number };

                case bool yes:
                    return new CardVisualValue { kind = CardVisualValueKind.Number, number = yes ? 1f : 0f };

                case Enum choice:
                    return new CardVisualValue
                    {
                        kind = CardVisualValueKind.Number,
                        number = Convert.ToInt32(choice, CultureInfo.InvariantCulture)
                    };

                case Vector2 point:
                    return new CardVisualValue
                    {
                        kind = CardVisualValueKind.Vector,
                        vector = new Vector4(point.x, point.y, 0f, 0f)
                    };

                case Vector3 point:
                    return new CardVisualValue
                    {
                        kind = CardVisualValueKind.Vector,
                        vector = new Vector4(point.x, point.y, point.z, 0f)
                    };

                case Rect rectangle:
                    return new CardVisualValue
                    {
                        kind = CardVisualValueKind.Vector,
                        vector = new Vector4(
                            rectangle.x, rectangle.y, rectangle.width, rectangle.height)
                    };

                case Color colour:
                    return new CardVisualValue { kind = CardVisualValueKind.Colour, colour = colour };

                case string words:
                    return new CardVisualValue { kind = CardVisualValueKind.Text, text = words };

                default:
                    return new CardVisualValue { kind = CardVisualValueKind.Text, text = string.Empty };
            }
        }

        /// <summary>Reads this back as the type a property wants, or null.</summary>
        public object As(Type wanted)
        {
            if (wanted == null)
            {
                return null;
            }

            if (wanted == typeof(float))
            {
                return number;
            }

            if (wanted == typeof(int))
            {
                return Mathf.RoundToInt(number);
            }

            if (wanted == typeof(bool))
            {
                return number != 0f;
            }

            if (wanted.IsEnum)
            {
                return Enum.ToObject(wanted, Mathf.RoundToInt(number));
            }

            if (wanted == typeof(Vector2))
            {
                return new Vector2(vector.x, vector.y);
            }

            if (wanted == typeof(Vector3))
            {
                return new Vector3(vector.x, vector.y, vector.z);
            }

            if (wanted == typeof(Rect))
            {
                return new Rect(vector.x, vector.y, vector.z, vector.w);
            }

            if (wanted == typeof(Color))
            {
                return colour;
            }

            if (wanted == typeof(string))
            {
                return text ?? string.Empty;
            }

            return null;
        }

        /// <summary>
        /// Which of the four fields a property of this type is stored in.
        ///
        /// Null for a type nothing here can hold, which is the honest answer
        /// and the one the validator reports.
        /// </summary>
        public static CardVisualValueKind? KindFor(Type wanted)
        {
            if (wanted == null)
            {
                return null;
            }

            if (wanted == typeof(float) || wanted == typeof(int) ||
                wanted == typeof(bool) || wanted.IsEnum)
            {
                return CardVisualValueKind.Number;
            }

            if (wanted == typeof(Vector2) || wanted == typeof(Vector3) || wanted == typeof(Rect))
            {
                return CardVisualValueKind.Vector;
            }

            if (wanted == typeof(Color))
            {
                return CardVisualValueKind.Colour;
            }

            if (wanted == typeof(string))
            {
                return CardVisualValueKind.Text;
            }

            return null;
        }

        /// <summary>
        /// Whether this was stored as the kind a property of that type is read
        /// from.
        ///
        /// Worth asking because <see cref="As"/> does not: it reads whichever
        /// field the wanted type lives in and never checks that anything was
        /// written there, so a colour saved against a number reads back as zero
        /// and looks exactly like an authored zero. That is the shape of
        /// malformed data this project would least like to debug, so it is
        /// reported rather than absorbed.
        /// </summary>
        public bool Fits(Type wanted)
        {
            CardVisualValueKind? expected = KindFor(wanted);

            return expected.HasValue && expected.Value == kind;
        }

        /// <summary>Whether this names a value the enumeration actually defines.</summary>
        public bool IsDefinedFor(Type wanted) =>
            wanted != null &&
            (!wanted.IsEnum || Enum.IsDefined(wanted, Enum.ToObject(wanted, Mathf.RoundToInt(number))));

        /// <summary>Whether two stored values say the same thing.</summary>
        public bool SameAs(in CardVisualValue other) =>
            kind == other.kind &&
            number.Equals(other.number) &&
            vector.Equals(other.vector) &&
            colour.Equals(other.colour) &&
            string.Equals(text ?? string.Empty, other.text ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>One thing one card does differently, named by layer and property.</summary>
    [Serializable]
    public sealed class CardVisualPropertyOverride
    {
        [Tooltip("The authoring label of the layer this adjusts.")]
        public string layer = string.Empty;

        [Tooltip("Which property of it, as the schema names it.")]
        public string property = string.Empty;

        public CardVisualValue value;
    }

    /// <summary>
    /// Everything one card does differently from the type it belongs to.
    ///
    /// Sparse, and that is the whole design. A card that wants its title a
    /// little wider stores one row: that layer, that property, that number.
    /// Everything else keeps coming from the recipe, so retuning the recipe
    /// still moves it — which is what makes a roster of a thousand cards
    /// maintainable and a roster of copies not.
    ///
    /// Nothing here knows what any property means. It is a list of names and
    /// values, and the schema is what turns a name back into a field.
    /// </summary>
    [Serializable]
    public sealed class CardVisualOverrides
    {
        [SerializeField]
        private List<CardVisualPropertyOverride> properties = new List<CardVisualPropertyOverride>();

        public IReadOnlyList<CardVisualPropertyOverride> Properties => properties;

        public bool IsEmpty => properties == null || properties.Count == 0;

        /// <summary>What this card says about one property of one layer, if anything.</summary>
        public bool TryGet(string layer, string property, out CardVisualValue found)
        {
            if (properties != null)
            {
                for (int index = 0; index < properties.Count; index++)
                {
                    CardVisualPropertyOverride row = properties[index];

                    if (row != null &&
                        string.Equals(row.layer, layer, StringComparison.Ordinal) &&
                        string.Equals(row.property, property, StringComparison.Ordinal))
                    {
                        found = row.value;
                        return true;
                    }
                }
            }

            found = default;
            return false;
        }

        public bool Overrides(string layer, string property) =>
            TryGet(layer, property, out _);

        /// <summary>
        /// The value this card asks for a property - already checked and
        /// converted - or nothing at all, if there is no row for it or the row
        /// is malformed.
        ///
        /// The one door every application of an override goes through, which
        /// is what makes it the one place two things are true at once:
        ///
        ///   a row saved under a property's current id, or under any id it
        ///   used to answer to, is found - the current id winning if, somehow,
        ///   both are present at once;
        ///
        ///   a row that is not a value of the property's own type - a colour
        ///   where a number belongs, an enumeration member that does not
        ///   exist - is treated exactly like no row at all. It does not throw,
        ///   and <see cref="CardVisualValue.As"/> is never asked to make
        ///   something plausible out of it. That is the runtime's whole safety
        ///   net: a malformed row is reported by the editor's validator, and
        ///   refused here, in a build where the validator was never opened.
        /// </summary>
        public bool TryResolve(string layer, CardVisualProperty property, out object value)
        {
            value = null;

            if (property == null)
            {
                return false;
            }

            if (!TryGet(layer, property.Id, out CardVisualValue stored) &&
                !TryFormerId(layer, property, out stored))
            {
                return false;
            }

            if (!stored.Fits(property.Type) || !stored.IsDefinedFor(property.Type))
            {
                return false;
            }

            value = stored.As(property.Type);
            return value != null;
        }

        private bool TryFormerId(string layer, CardVisualProperty property, out CardVisualValue found)
        {
            IReadOnlyList<string> formerIds = property.FormerIds;

            for (int index = 0; index < formerIds.Count; index++)
            {
                if (TryGet(layer, formerIds[index], out found))
                {
                    return true;
                }
            }

            found = default;
            return false;
        }

        /// <summary>How many things this card asks for. Diagnostics and reports.</summary>
        public int Count => properties?.Count ?? 0;

        /// <summary>
        /// How many times this has been edited since it was loaded.
        ///
        /// Not serialised, and not a version number anyone authors. It exists
        /// because the editor mutates one of these *in place*: the library
        /// hands out the same object every time, so a view holding a
        /// description of a card holds the very object that just changed, and
        /// no comparison of contents between two references to one object can
        /// ever report a difference. A stamp taken when the description was
        /// built is the only thing that can.
        /// </summary>
        [NonSerialized] private int _revision;

        public int Revision => _revision;

        /// <summary>
        /// Whether two sets ask for exactly the same things.
        ///
        /// Order-insensitive: two sets holding the same rows describe the same
        /// card however they were built up. Null and empty are the same thing,
        /// because a card with no adjustments and a card with an empty set of
        /// them compose identically.
        /// </summary>
        public static bool SameContent(CardVisualOverrides left, CardVisualOverrides right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;

            if (leftCount != rightCount)
            {
                return false;
            }

            if (leftCount == 0)
            {
                return true;
            }

            for (int index = 0; index < left.properties.Count; index++)
            {
                CardVisualPropertyOverride row = left.properties[index];

                if (row == null)
                {
                    continue;
                }

                if (!right.TryGet(row.layer, row.property, out CardVisualValue theirs) ||
                    !row.value.SameAs(theirs))
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Records one adjustment, replacing any it already had.</summary>
        internal void Set(string layer, string property, CardVisualValue value)
        {
            properties ??= new List<CardVisualPropertyOverride>();

            for (int index = 0; index < properties.Count; index++)
            {
                CardVisualPropertyOverride row = properties[index];

                if (row != null &&
                    string.Equals(row.layer, layer, StringComparison.Ordinal) &&
                    string.Equals(row.property, property, StringComparison.Ordinal))
                {
                    row.value = value;
                    _revision++;
                    return;
                }
            }

            properties.Add(new CardVisualPropertyOverride
            {
                layer = layer,
                property = property,
                value = value
            });

            _revision++;
        }

        /// <summary>
        /// Records one adjustment under its current id, and forgets any row
        /// still sitting under an id it used to answer to.
        ///
        /// Every write from the editor goes through here rather than through
        /// the raw string overload, which is what keeps "the current id is the
        /// only persisted target" true instead of aspirational: a property
        /// found through a former id and then edited leaves this card with one
        /// row, under the current name, not two.
        /// </summary>
        internal void Set(string layer, CardVisualProperty property, CardVisualValue value)
        {
            Set(layer, property.Id, value);

            IReadOnlyList<string> formerIds = property.FormerIds;

            for (int index = 0; index < formerIds.Count; index++)
            {
                Clear(layer, formerIds[index]);
            }
        }

        /// <summary>Forgets this card's adjustment, under its current id or any former one.</summary>
        internal void Clear(string layer, CardVisualProperty property)
        {
            Clear(layer, property.Id);

            IReadOnlyList<string> formerIds = property.FormerIds;

            for (int index = 0; index < formerIds.Count; index++)
            {
                Clear(layer, formerIds[index]);
            }
        }

        /// <summary>Forgets one adjustment, so the card inherits again.</summary>
        internal void Clear(string layer, string property)
        {
            if (properties == null)
            {
                return;
            }

            for (int index = properties.Count - 1; index >= 0; index--)
            {
                CardVisualPropertyOverride row = properties[index];

                if (row != null &&
                    string.Equals(row.layer, layer, StringComparison.Ordinal) &&
                    string.Equals(row.property, property, StringComparison.Ordinal))
                {
                    properties.RemoveAt(index);
                    _revision++;
                }
            }
        }

        /// <summary>Forgets everything this card asked for about one layer.</summary>
        internal void ClearLayer(string layer)
        {
            if (properties == null)
            {
                return;
            }

            for (int index = properties.Count - 1; index >= 0; index--)
            {
                if (properties[index] != null &&
                    string.Equals(properties[index].layer, layer, StringComparison.Ordinal))
                {
                    properties.RemoveAt(index);
                    _revision++;
                }
            }
        }

        /// <summary>Forgets everything.</summary>
        internal void Clear()
        {
            properties?.Clear();
            _revision++;
        }
#endif
    }
}
