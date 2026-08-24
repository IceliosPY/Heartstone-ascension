using System;
using CoH.Core.Identifiers;

namespace CoH.Core.State
{
    /// <summary>
    /// Anything that exists as an addressable object during a match: a hero, a
    /// minion, a card in a zone.
    ///
    /// Every entity carries a stable <see cref="Id"/> because commands and
    /// events refer to entities by id rather than by object reference. That is
    /// what will later let the exact same command travel over the network to
    /// an authoritative server.
    ///
    /// Mutable members use internal setters: the public surface is read-only,
    /// and only the engine (and its tests) may change state.
    /// </summary>
    public abstract class Entity
    {
        private protected Entity(EntityId id, PlayerId owner)
        {
            if (id.IsNone)
            {
                throw new ArgumentException("An entity needs a real id.", nameof(id));
            }

            if (owner.IsNone)
            {
                throw new ArgumentException("An entity needs a real owner.", nameof(owner));
            }

            Id = id;
            Owner = owner;
            Controller = owner;
        }

        public EntityId Id { get; }

        /// <summary>
        /// The player this entity originally belongs to. Never changes, even
        /// when control is stolen: a mind-controlled minion still returns to
        /// its owner's graveyard when it dies.
        /// </summary>
        public PlayerId Owner { get; }

        /// <summary>
        /// The player currently commanding this entity. Starts equal to
        /// <see cref="Owner"/> and can change through control effects.
        /// </summary>
        public PlayerId Controller { get; internal set; }

        /// <summary>
        /// Monotonic order of entry into play, handed out by
        /// GameState.NextTimestamp.
        ///
        /// Needed because several Hearthstone behaviours depend on the order
        /// entities arrived rather than on their board position: which trigger
        /// resolves first when two react to the same event, in which order
        /// auras apply, and which minion counts as the oldest. Zero means the
        /// entity has not entered play yet.
        /// </summary>
        public int Timestamp { get; internal set; }
    }
}
