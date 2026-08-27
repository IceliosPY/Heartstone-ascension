using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// What kind of thing the pointer is over.
    ///
    /// Friendly and enemy here mean nothing more than "belongs to whoever is
    /// acting" and "does not". It is how an interaction is routed, never how a
    /// rule is decided: whether an enemy minion may actually be attacked is a
    /// question only the engine answers, and this enum is not part of the
    /// answer.
    /// </summary>
    public enum PointerTargetKind
    {
        None = 0,
        HandCard = 1,
        FriendlyMinion = 2,
        EnemyMinion = 3,
        FriendlyHero = 4,
        EnemyHero = 5,
        BoardDropZone = 6
    }

    /// <summary>One thing under the pointer, already identified.</summary>
    public readonly struct PointerHit
    {
        public PointerHit(
            PointerTargetKind kind, EntityId entityId, Vector3 point, float distance,
            Collider collider, CardView card, MinionView minion, HeroView hero, BoardDropZone zone)
        {
            Kind = kind;
            EntityId = entityId;
            Point = point;
            Distance = distance;
            Collider = collider;
            Card = card;
            Minion = minion;
            Hero = hero;
            Zone = zone;
        }

        public PointerTargetKind Kind { get; }

        /// <summary>The engine entity behind it, or None for a drop zone.</summary>
        public EntityId EntityId { get; }

        public Vector3 Point { get; }

        public float Distance { get; }

        public Collider Collider { get; }

        public CardView Card { get; }

        public MinionView Minion { get; }

        public HeroView Hero { get; }

        public BoardDropZone Zone { get; }

        public bool IsMinion => Kind == PointerTargetKind.FriendlyMinion || Kind == PointerTargetKind.EnemyMinion;

        public bool IsHero => Kind == PointerTargetKind.FriendlyHero || Kind == PointerTargetKind.EnemyHero;

        public string Describe() => Collider == null
            ? Kind.ToString()
            : Kind + " " + EntityId + " (" + Collider.name + ")";
    }
}
