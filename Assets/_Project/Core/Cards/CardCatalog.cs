using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;

namespace CoH.Core.Cards
{
    /// <summary>
    /// In-memory card catalog built once from a set of definitions.
    ///
    /// Note on determinism: the internal dictionary is used for lookups only.
    /// Nothing in the engine may enumerate it when order matters, because hash
    /// ordering is not guaranteed stable. Any future "all cards" query must
    /// return a deliberately sorted list.
    /// </summary>
    public sealed class CardCatalog : ICardCatalog
    {
        private readonly Dictionary<CardId, CardDefinition> _definitionsById;
        private readonly CardDefinition[] _ordered;

        public CardCatalog(IEnumerable<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitionsById = new Dictionary<CardId, CardDefinition>();

            foreach (CardDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("The catalog cannot contain a null definition.", nameof(definitions));
                }

                if (_definitionsById.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Duplicate card id in catalog: " + definition.Id, nameof(definitions));
                }

                _definitionsById.Add(definition.Id, definition);
            }

            // Sorted once here rather than at every call site. A dictionary has
            // no order worth relying on, and everything that reads a catalog as
            // a sequence needs one that never changes.
            _ordered = new CardDefinition[_definitionsById.Count];
            _definitionsById.Values.CopyTo(_ordered, 0);
            Array.Sort(_ordered, (left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));
        }

        public int Count => _definitionsById.Count;

        /// <inheritdoc />
        public IReadOnlyList<CardDefinition> Cards => _ordered;

        public bool TryGet(CardId id, out CardDefinition definition) =>
            _definitionsById.TryGetValue(id, out definition);

        public CardDefinition Get(CardId id)
        {
            if (!_definitionsById.TryGetValue(id, out CardDefinition definition))
            {
                throw new KeyNotFoundException("Unknown card id: " + id);
            }

            return definition;
        }
    }
}
