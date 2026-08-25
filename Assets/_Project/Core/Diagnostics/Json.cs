using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// A very small JSON reader and writer.
    ///
    /// Hand written because CoH.Core is plain C# with no Unity in it, which
    /// rules out JsonUtility, and because pulling in a serialisation library
    /// for one debug file format would be a large dependency bought for a small
    /// need. What is here is the subset a replay file uses and nothing more:
    /// objects, arrays, strings, numbers, booleans and null.
    ///
    /// The writer indents, because the whole point of choosing a text format
    /// was that a person can open the file and read it.
    /// </summary>
    internal sealed class JsonValue
    {
        private readonly Dictionary<string, JsonValue> _members;
        private readonly List<JsonValue> _items;
        private readonly string _text;
        private readonly double _number;
        private readonly bool _boolean;
        private readonly JsonKind _kind;

        private enum JsonKind
        {
            Null,
            Object,
            Array,
            String,
            Number,
            Boolean
        }

        private JsonValue(JsonKind kind, Dictionary<string, JsonValue> members = null,
            List<JsonValue> items = null, string text = null, double number = 0, bool boolean = false)
        {
            _kind = kind;
            _members = members;
            _items = items;
            _text = text;
            _number = number;
            _boolean = boolean;
        }

        public bool IsNull => _kind == JsonKind.Null;

        public IReadOnlyList<JsonValue> Items =>
            _items ?? (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();

        /// <summary>A member of an object, or a null value when absent.</summary>
        public JsonValue this[string name] =>
            _members != null && _members.TryGetValue(name, out JsonValue found)
                ? found
                : new JsonValue(JsonKind.Null);

        public bool Has(string name) => _members != null && _members.ContainsKey(name);

        public string AsString(string fallback = "") => _kind == JsonKind.String ? _text : fallback;

        public int AsInt(int fallback = 0) =>
            _kind == JsonKind.Number ? (int)Math.Round(_number) : fallback;

        public bool AsBool(bool fallback = false) => _kind == JsonKind.Boolean ? _boolean : fallback;

        /// <summary>
        /// A 64 bit unsigned value, read from the text form.
        ///
        /// Seeds are written as strings on purpose: a double cannot hold every
        /// ulong exactly, and a seed that came back off by one would produce a
        /// completely different match with no visible reason.
        /// </summary>
        public ulong AsULong(ulong fallback = 0UL)
        {
            if (_kind == JsonKind.String && ulong.TryParse(_text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed))
            {
                return parsed;
            }

            return _kind == JsonKind.Number ? (ulong)Math.Round(_number) : fallback;
        }

        // ------------------------------------------------------------------
        //  Reading
        // ------------------------------------------------------------------

        public static JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            int position = 0;
            JsonValue value = ParseValue(text, ref position);
            SkipWhitespace(text, ref position);

            return value;
        }

        private static JsonValue ParseValue(string text, ref int position)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length)
            {
                throw new FormatException("The replay file ends unexpectedly.");
            }

            char current = text[position];

            switch (current)
            {
                case '{':
                    return ParseObject(text, ref position);
                case '[':
                    return ParseArray(text, ref position);
                case '"':
                    return new JsonValue(JsonKind.String, text: ParseString(text, ref position));
                case 't':
                    Expect(text, ref position, "true");
                    return new JsonValue(JsonKind.Boolean, boolean: true);
                case 'f':
                    Expect(text, ref position, "false");
                    return new JsonValue(JsonKind.Boolean, boolean: false);
                case 'n':
                    Expect(text, ref position, "null");
                    return new JsonValue(JsonKind.Null);
                default:
                    return new JsonValue(JsonKind.Number, number: ParseNumber(text, ref position));
            }
        }

        private static JsonValue ParseObject(string text, ref int position)
        {
            Dictionary<string, JsonValue> members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            position++;

            SkipWhitespace(text, ref position);

            if (position < text.Length && text[position] == '}')
            {
                position++;
                return new JsonValue(JsonKind.Object, members);
            }

            while (true)
            {
                SkipWhitespace(text, ref position);
                string name = ParseString(text, ref position);

                SkipWhitespace(text, ref position);
                Require(text, ref position, ':');

                members[name] = ParseValue(text, ref position);

                SkipWhitespace(text, ref position);

                if (position >= text.Length)
                {
                    throw new FormatException("An object in the replay file is not closed.");
                }

                if (text[position] == ',')
                {
                    position++;
                    continue;
                }

                Require(text, ref position, '}');
                return new JsonValue(JsonKind.Object, members);
            }
        }

        private static JsonValue ParseArray(string text, ref int position)
        {
            List<JsonValue> items = new List<JsonValue>();
            position++;

            SkipWhitespace(text, ref position);

            if (position < text.Length && text[position] == ']')
            {
                position++;
                return new JsonValue(JsonKind.Array, items: items);
            }

            while (true)
            {
                items.Add(ParseValue(text, ref position));
                SkipWhitespace(text, ref position);

                if (position >= text.Length)
                {
                    throw new FormatException("An array in the replay file is not closed.");
                }

                if (text[position] == ',')
                {
                    position++;
                    continue;
                }

                Require(text, ref position, ']');
                return new JsonValue(JsonKind.Array, items: items);
            }
        }

        private static string ParseString(string text, ref int position)
        {
            Require(text, ref position, '"');

            StringBuilder value = new StringBuilder();

            while (position < text.Length)
            {
                char current = text[position++];

                if (current == '"')
                {
                    return value.ToString();
                }

                if (current != '\\')
                {
                    value.Append(current);
                    continue;
                }

                if (position >= text.Length)
                {
                    break;
                }

                char escaped = text[position++];

                switch (escaped)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length)
                        {
                            throw new FormatException("A truncated escape in the replay file.");
                        }

                        value.Append((char)ushort.Parse(
                            text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default:
                        throw new FormatException("Unknown escape sequence in the replay file: \\" + escaped);
                }
            }

            throw new FormatException("A string in the replay file is not closed.");
        }

        private static double ParseNumber(string text, ref int position)
        {
            int start = position;

            while (position < text.Length && "+-.eE0123456789".IndexOf(text[position]) >= 0)
            {
                position++;
            }

            string slice = text.Substring(start, position - start);

            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new FormatException("Not a number in the replay file: " + slice);
            }

            return value;
        }

        private static void SkipWhitespace(string text, ref int position)
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }

        private static void Require(string text, ref int position, char expected)
        {
            SkipWhitespace(text, ref position);

            if (position >= text.Length || text[position] != expected)
            {
                throw new FormatException(
                    "Expected '" + expected + "' at character " + position + " of the replay file.");
            }

            position++;
        }

        private static void Expect(string text, ref int position, string literal)
        {
            if (position + literal.Length > text.Length ||
                string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Expected '" + literal + "' at character " + position + ".");
            }

            position += literal.Length;
        }
    }

    /// <summary>Builds indented JSON text.</summary>
    internal sealed class JsonWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private int _depth;
        private bool _needsComma;

        public JsonWriter BeginObject(string name = null)
        {
            Separate(name);
            _text.Append('{');
            _depth++;
            _needsComma = false;
            return this;
        }

        public JsonWriter EndObject()
        {
            _depth--;
            NewLine();
            _text.Append('}');
            _needsComma = true;
            return this;
        }

        public JsonWriter BeginArray(string name = null)
        {
            Separate(name);
            _text.Append('[');
            _depth++;
            _needsComma = false;
            return this;
        }

        public JsonWriter EndArray()
        {
            _depth--;
            NewLine();
            _text.Append(']');
            _needsComma = true;
            return this;
        }

        public JsonWriter Write(string name, string value)
        {
            Separate(name);
            WriteQuoted(value);
            _needsComma = true;
            return this;
        }

        public JsonWriter Write(string name, int value)
        {
            Separate(name);
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
            _needsComma = true;
            return this;
        }

        public JsonWriter Write(string name, bool value)
        {
            Separate(name);
            _text.Append(value ? "true" : "false");
            _needsComma = true;
            return this;
        }

        public JsonWriter WriteRaw(string value)
        {
            Separate(null);
            WriteQuoted(value);
            _needsComma = true;
            return this;
        }

        public override string ToString() => _text.ToString();

        private void Separate(string name)
        {
            if (_needsComma)
            {
                _text.Append(',');
            }

            NewLine();

            if (name != null)
            {
                WriteQuoted(name);
                _text.Append(": ");
            }
        }

        private void NewLine()
        {
            if (_text.Length == 0)
            {
                return;
            }

            _text.Append('\n');
            _text.Append(' ', _depth * 2);
        }

        private void WriteQuoted(string value)
        {
            _text.Append('"');

            string safe = value ?? string.Empty;

            for (int index = 0; index < safe.Length; index++)
            {
                char current = safe[index];

                switch (current)
                {
                    case '"': _text.Append("\\\""); break;
                    case '\\': _text.Append("\\\\"); break;
                    case '\n': _text.Append("\\n"); break;
                    case '\r': _text.Append("\\r"); break;
                    case '\t': _text.Append("\\t"); break;
                    default:
                        if (current < ' ')
                        {
                            _text.Append("\\u").Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _text.Append(current);
                        }

                        break;
                }
            }

            _text.Append('"');
        }
    }
}
