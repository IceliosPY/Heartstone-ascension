using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Core.State;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The one place that turns a ray into "what is under the pointer".
    ///
    /// Every view is silent about the mouse. Nothing raycasts on its own, so
    /// there is exactly one set of rules for how a click is resolved, and one
    /// place to change when the board grows a new kind of object.
    ///
    /// It reports everything the ray passes through, nearest first, rather than
    /// only the closest thing. That matters because what counts as the target
    /// depends on what the player is doing: while a card is being dragged the
    /// board underneath is what is being aimed at, even though the near hero
    /// stands in front of part of it, and while an attack is being aimed the
    /// minions matter and the board behind them does not. Picking from a list
    /// keeps decorative and overlapping geometry from swallowing clicks, which
    /// is what shrinking colliders until nothing overlaps would otherwise cost.
    ///
    /// It also decides nothing. Friendly and enemy are read off the state so an
    /// interaction can be routed; whether the interaction is legal is asked of
    /// the engine afterwards, every time.
    /// </summary>
    public sealed class PointerProbe
    {
        private const int MaxHits = 24;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[MaxHits];
        private readonly List<PointerHit> _hits = new List<PointerHit>(MaxHits);

        /// <summary>Everything the last probe passed through, nearest first.</summary>
        public IReadOnlyList<PointerHit> Hits => _hits;

        /// <summary>
        /// Fires a ray and identifies everything it meets.
        /// </summary>
        /// <param name="acting">
        /// Whoever holds the turn. Only used to label hits friendly or enemy.
        /// </param>
        public IReadOnlyList<PointerHit> Probe(Ray ray, LayerMask mask, GameState state, PlayerId acting)
        {
            _hits.Clear();

            // Views move every frame while a card is hovered or dragged, and
            // Unity does not push those moves into the physics scene by itself.
            // Without this the pointer would test against last frame's world.
            Physics.SyncTransforms();

            int count = Physics.RaycastNonAlloc(ray, _raycastHits, 200f, mask);

            for (int index = 0; index < count; index++)
            {
                _hits.Add(Identify(_raycastHits[index], state, acting));
            }

            _hits.Sort(CompareByDistance);
            return _hits;
        }

        /// <summary>The nearest hit of the given kind, if the ray met one.</summary>
        public bool TryFind(PointerTargetKind kind, out PointerHit found)
        {
            for (int index = 0; index < _hits.Count; index++)
            {
                if (_hits[index].Kind == kind)
                {
                    found = _hits[index];
                    return true;
                }
            }

            found = default;
            return false;
        }

        /// <summary>
        /// The nearest thing that could be attacked: any minion or hero,
        /// friendly or not. Whether it is a legal target is asked separately, so
        /// that releasing on a friendly minion cancels rather than doing nothing
        /// visible.
        /// </summary>
        public bool TryFindCharacter(out PointerHit found)
        {
            for (int index = 0; index < _hits.Count; index++)
            {
                if (_hits[index].IsMinion || _hits[index].IsHero)
                {
                    found = _hits[index];
                    return true;
                }
            }

            found = default;
            return false;
        }

        /// <summary>The nearest hit of any kind, or a None hit when the ray met nothing.</summary>
        public PointerHit Nearest => _hits.Count > 0 ? _hits[0] : default;

        private static int CompareByDistance(PointerHit left, PointerHit right) =>
            left.Distance.CompareTo(right.Distance);

        private static PointerHit Identify(RaycastHit hit, GameState state, PlayerId acting)
        {
            Collider collider = hit.collider;

            CardView card = collider.GetComponentInParent<CardView>();
            if (card != null)
            {
                // A face down card is the waiting player's, and stands for a
                // count rather than for something anybody can pick up.
                PointerTargetKind kind = card.IsFaceDown || card.EntityId.IsNone
                    ? PointerTargetKind.None
                    : PointerTargetKind.HandCard;

                return new PointerHit(kind, card.EntityId, hit.point, hit.distance,
                    collider, card, null, null, null);
            }

            MinionView minion = collider.GetComponentInParent<MinionView>();
            if (minion != null)
            {
                bool friendly = ControllerOf(state, minion.EntityId) == acting;

                return new PointerHit(
                    friendly ? PointerTargetKind.FriendlyMinion : PointerTargetKind.EnemyMinion,
                    minion.EntityId, hit.point, hit.distance, collider, null, minion, null, null);
            }

            HeroView hero = collider.GetComponentInParent<HeroView>();
            if (hero != null)
            {
                bool friendly = hero.PlayerId == acting;

                return new PointerHit(
                    friendly ? PointerTargetKind.FriendlyHero : PointerTargetKind.EnemyHero,
                    hero.EntityId, hit.point, hit.distance, collider, null, null, hero, null);
            }

            BoardDropZone zone = collider.GetComponentInParent<BoardDropZone>();
            if (zone != null)
            {
                return new PointerHit(PointerTargetKind.BoardDropZone, EntityId.None, hit.point,
                    hit.distance, collider, null, null, null, zone);
            }

            return new PointerHit(PointerTargetKind.None, EntityId.None, hit.point, hit.distance,
                collider, null, null, null, null);
        }

        private static PlayerId ControllerOf(GameState state, EntityId id)
        {
            if (state != null && state.TryGetEntity(id, out Entity entity) && entity is Minion minion)
            {
                return minion.Controller;
            }

            return PlayerId.None;
        }
    }
}
