using CoH.Core.Identifiers;

namespace CoH.Core.Cards
{
    /// <summary>
    /// Read-only lookup from a <see cref="CardId"/> to its definition.
    ///
    /// The engine depends on this interface rather than on a concrete source
    /// so that definitions can come from Unity ScriptableObjects in the editor
    /// and from plain data on a future headless server, without the rules
    /// layer ever knowing the difference.
    /// </summary>
    public interface ICardCatalog
    {
        bool TryGet(CardId id, out CardDefinition definition);

        /// <summary>Returns the definition, or throws if the id is unknown.</summary>
        CardDefinition Get(CardId id);
    }
}
