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
        }

        public int Count => _definitionsById.Count;

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
