using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Keeps the scene showing what the engine says.
    ///
    /// The screen has a near side and a far side, and the near side belongs to
    /// whoever is acting. That is the whole hotseat model: there is no permanent
    /// human seat, so the comfortable half of the screen follows the turn rather
    /// than a player number. Both players therefore see their hand in the same
    /// place, at the same size, reachable by the same click.
    ///
    /// After each batch of events it reconciles the views against the state:
    /// spawn what appeared, remove what died, refresh every number, re-place
    /// everything. Reconciling rather than mutating blindly means the scene
    /// cannot drift out of step with the rules.
    ///
    /// It no longer stages events itself. Animations belong to the visualizers,
    /// which drive the views over time; what is left here is the layout they
    /// animate toward, and one reconcile once a batch has finished, as a safety
    /// net rather than as the mechanism.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameSession session;
        [SerializeField] private BoardAnchors anchors;
        [SerializeField] private MatchHud hud;

        [Header("Prefabs")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private MinionView minionPrefab;

        [Header("Heroes")]
        [SerializeField] private HeroView nearHero;
        [SerializeField] private HeroView farHero;

        [Header("Interaction")]
        [Tooltip("Where a card being dragged is parented, clear of both hands.")]
        [SerializeField] private Transform dragLayer;

        [SerializeField] private BoardInsertionMarker insertionMarker;

        [Header("Layout")]
        [SerializeField] private HandFanSettings handLayout = new HandFanSettings();
        [SerializeField] private float boardSpacing = 1.2f;

        [Tooltip("How much smaller the waiting player's hand is drawn.")]
        [SerializeField] private float farHandScale = 0.55f;

        private readonly Dictionary<EntityId, CardView> _cardViews = new Dictionary<EntityId, CardView>();
        private readonly Dictionary<EntityId, MinionView> _minionViews = new Dictionary<EntityId, MinionView>();
        private readonly List<EntityId> _scratch = new List<EntityId>();

        private PlayerId _viewpoint = PlayerId.None;
        private EntityId _draggedCard = EntityId.None;
        private int _insertionSlot = -1;

        public GameSession Session => session;

        /// <summary>Where a card being dragged lives while it follows the pointer.</summary>
        public Transform DragLayer => dragLayer;

        /// <summary>The acting player's hand anchor, which a dropped card returns to.</summary>
        public Transform NearHandAnchor => anchors == null ? null : anchors.Hand(true);

        /// <summary>The acting player's minion row, whose space the drop preview is computed in.</summary>
        public Transform NearBoardAnchor => anchors == null ? null : anchors.Board(true);

        /// <summary>Gap between two neighbouring minions, shared with the drop resolver.</summary>
        public float BoardSpacing => boardSpacing;

        /// <summary>Which slot the drop preview is currently holding open, or -1.</summary>
        public int InsertionSlot => _insertionSlot;

        /// <summary>Whose side of the screen is the near one right now.</summary>
        public PlayerId Viewpoint => _viewpoint;

        public bool TryGetCardView(EntityId id, out CardView view) => _cardViews.TryGetValue(id, out view);

        public bool TryGetMinionView(EntityId id, out MinionView view) => _minionViews.TryGetValue(id, out view);

        public IReadOnlyDictionary<EntityId, MinionView> MinionViews => _minionViews;

        public IReadOnlyDictionary<EntityId, CardView> CardViews => _cardViews;

        /// <summary>Finds the hero view currently showing the given entity, if any.</summary>
        public bool TryGetHeroView(EntityId id, out HeroView view)
        {
            if (nearHero != null && nearHero.EntityId == id)
            {
                view = nearHero;
                return true;
            }

            if (farHero != null && farHero.EntityId == id)
            {
                view = farHero;
                return true;
            }

            view = null;
            return false;
        }

        /// <summary>The hero view currently showing this seat, whichever side it is on.</summary>
        public bool TryGetHeroViewOf(PlayerId seat, out HeroView view)
        {
            if (nearHero != null && nearHero.PlayerId == seat)
            {
                view = nearHero;
                return true;
            }

            if (farHero != null && farHero.PlayerId == seat)
            {
                view = farHero;
                return true;
            }

            view = null;
            return false;
        }

        public HeroView NearHero => nearHero;

        public HeroView FarHero => farHero;

        /// <summary>Anchors, so a staged event can find the deck or a row.</summary>
        public BoardAnchors Anchors => anchors;

        /// <summary>True when this seat currently owns the near side of the screen.</summary>
        public bool IsNear(PlayerId seat) => seat == _viewpoint;

        /// <summary>Lays out the two rows and nothing else.</summary>
        public void RelayoutBoards()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }

            GameState state = session.State;
            PlayerId near = _viewpoint.IsNone ? PlayerId.One : _viewpoint;

            // Places what is on the board and removes nothing. A minion the
            // engine has already taken away may still be mid death animation,
            // and sweeping it here would delete the second half of a trade
            // before anyone saw it die. Removal belongs to the death that is
            // being staged; the reconcile at the end of the batch is the net.
            RebuildBoard(state, near, true);
            RebuildBoard(state, near.Opponent, false);
            RefreshInsertionMarker(state, near);

            Physics.SyncTransforms();
        }

        /// <summary>Lays out the two hands and nothing else.</summary>
        public void RelayoutHands()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }

            GameState state = session.State;
            PlayerId near = _viewpoint.IsNone ? PlayerId.One : _viewpoint;

            // Same rule as the rows: a card on its way out is removed by the
            // animation showing it leave, not by a layout pass.
            RebuildHand(state, near, true);
            RebuildHand(state, near.Opponent, false);

            Physics.SyncTransforms();
        }

        /// <summary>Refreshes the two hero plates from the current state.</summary>
        public void RefreshHeroes()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }

            GameState state = session.State;
            PlayerId near = _viewpoint.IsNone ? PlayerId.One : _viewpoint;

            if (nearHero != null)
            {
                nearHero.Bind(state.GetPlayer(near), MatchHud.Describe(near), true);
            }

            if (farHero != null)
            {
                farHero.Bind(state.GetPlayer(near.Opponent), MatchHud.Describe(near.Opponent), false);
            }
        }

        /// <summary>Refreshes the readout without touching anything on the table.</summary>
        public void RefreshHud()
        {
            if (hud != null && session != null && session.IsReady)
            {
                hud.Refresh(session.State);
            }
        }

        /// <summary>
        /// Creates a card view off to one side, for an event that wants to bring
        /// a card in from somewhere. It is parented to the drag layer, so no
        /// hand layout moves it until the animation hands it over.
        /// </summary>
        public CardView SpawnLooseCardView(EntityId card, PlayerId seat)
        {
            if (_cardViews.TryGetValue(card, out CardView existing) && existing != null)
            {
                return existing;
            }

            CardView view = Instantiate(cardPrefab, dragLayer);
            _cardViews[card] = view;

            BindCardView(view, card, seat);
            return view;
        }

        /// <summary>Shows a card as its seat should see it: face up only for the near side.</summary>
        public void BindCardView(CardView view, EntityId card, PlayerId seat)
        {
            if (view == null || session == null || !session.IsReady)
            {
                return;
            }

            GameState state = session.State;

            if (IsNear(seat) && state.TryGetEntity(card, out Entity entity) && entity is CardInstance instance)
            {
                view.Bind(BuildCardModel(state, instance, seat));
            }
            else
            {
                view.BindFaceDown();
            }
        }

        /// <summary>
        /// Where a card of this hand will end up once the fan settles, in world
        /// space. What a travelling card aims at, so it arrives exactly where
        /// the layout was going to put it and nothing jumps afterwards.
        /// </summary>
        public bool TryGetHandPose(
            PlayerId seat, EntityId card, out Vector3 position, out Quaternion rotation, out float scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = handLayout.Scale;

            if (session == null || !session.IsReady || anchors == null)
            {
                return false;
            }

            bool near = IsNear(seat);
            Transform anchor = anchors.Hand(near);
            Player player = session.State.GetPlayer(seat);

            int index = IndexInHand(player, card);

            if (index < 0 || anchor == null)
            {
                return false;
            }

            CardPose pose = HandFanLayout.GetPose(index, player.Hand.Count, handLayout);
            scale = handLayout.Scale * (near ? 1f : farHandScale);

            position = anchor.TransformPoint(pose.LocalPosition);
            rotation = anchor.rotation * pose.LocalRotation;
            return true;
        }

        /// <summary>Where a deck sits, which is where a drawn card comes from.</summary>
        public Vector3 DeckPosition(PlayerId seat)
        {
            Transform deck = anchors == null ? null : anchors.Deck(IsNear(seat));
            return deck == null ? Vector3.zero : deck.position;
        }

        /// <summary>Forgets a card view and destroys it.</summary>
        public void RemoveCardView(EntityId card) => Despawn(card);

        /// <summary>Forgets a minion view and destroys it.</summary>
        public void RemoveMinionView(EntityId minion) => Despawn(minion);

        private static int IndexInHand(Player player, EntityId card)
        {
            for (int index = 0; index < player.Hand.Count; index++)
            {
                if (player.Hand[index].Id == card)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Tells the hand that one of its cards is currently in the air.
        ///
        /// The card is still in the engine's hand, and stays there until the
        /// engine says otherwise. This only takes it out of the fan, so the
        /// others close the gap behind it exactly as they would if it had been
        /// played, and open it again if it comes back.
        /// </summary>
        public void SetDraggedCard(EntityId card)
        {
            if (_draggedCard == card)
            {
                return;
            }

            _draggedCard = card;
            Rebuild();
        }

        /// <summary>
        /// Holds a slot open in the acting player's row, or -1 to close it.
        ///
        /// Called only when the slot actually changes, not on every mouse move.
        /// </summary>
        public void SetInsertionPreview(int slot)
        {
            if (_insertionSlot == slot)
            {
                return;
            }

            _insertionSlot = slot;
            Rebuild();
        }

        private void OnEnable()
        {
            if (session != null && session.Queue != null)
            {
                session.Queue.Drained += Rebuild;
            }
        }

        private void OnDisable()
        {
            if (session != null && session.Queue != null)
            {
                session.Queue.Drained -= Rebuild;
            }
        }

        /// <summary>
        /// Brings the whole scene in line with the state. Used for the opening
        /// snapshot and after every batch of events.
        /// </summary>
        public void Rebuild()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }

            GameState state = session.State;

            // The acting player owns the near side. When the match is over there
            // is no acting player, so the last point of view is kept rather than
            // snapping the board around at the final moment.
            if (!state.CurrentPlayer.IsNone)
            {
                _viewpoint = state.CurrentPlayer;
            }
            else if (_viewpoint.IsNone)
            {
                _viewpoint = PlayerId.One;
            }

            PlayerId near = _viewpoint;
            PlayerId far = near.Opponent;

            RebuildBoard(state, near, true);
            RebuildBoard(state, far, false);

            RefreshInsertionMarker(state, near);

            RebuildHand(state, near, true);
            RebuildHand(state, far, false);

            DespawnMissingMinions(state);
            DespawnMissingCards(state);

            if (nearHero != null)
            {
                nearHero.Bind(state.GetPlayer(near), MatchHud.Describe(near), true);
            }

            if (farHero != null)
            {
                farHero.Bind(state.GetPlayer(far), MatchHud.Describe(far), false);
            }

            if (hud != null)
            {
                hud.Refresh(state);
            }

            // A reconcile means the board is idle, so nothing may still be
            // leaning out of its slot. The combat sequence clears its own lean
            // when it finishes; this is the net under it, so a future animation
            // that forgets cannot leave a minion permanently displaced.
            ClearLungeOffsets();

            // Everything above moved colliders, and clicking raycasts against
            // them. Unity does not push transform changes into the physics
            // scene on its own, so without this the first click after a turn
            // change would test against where the cards used to be, and miss.
            Physics.SyncTransforms();
        }

        private void ClearLungeOffsets()
        {
            foreach (KeyValuePair<EntityId, MinionView> pair in _minionViews)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetLungeOffset(Vector3.zero);
                }
            }
        }

        private void RebuildBoard(GameState state, PlayerId seat, bool near)
        {
            Player player = state.GetPlayer(seat);
            Transform anchor = anchors.Board(near);

            // Only the acting player's row opens up for a drop, and only while
            // one is being previewed.
            int gap = near ? _insertionSlot : -1;

            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                Minion minion = player.Board[slot];

                if (!_minionViews.TryGetValue(minion.Id, out MinionView view) || view == null)
                {
                    view = Instantiate(minionPrefab, anchor);
                    _minionViews[minion.Id] = view;
                }

                if (view.transform.parent != anchor)
                {
                    view.transform.SetParent(anchor, false);
                }

                // A target rather than a placement: the view slides there, so a
                // summon opens the row instead of teleporting its neighbours.
                view.SetRestingPose(
                    BoardDropResolver.PositionWithGap(slot, player.Board.Count, gap, boardSpacing));

                view.transform.localRotation = Quaternion.identity;

                view.Bind(BuildMinionModel(state, minion));
            }
        }

        private void RefreshInsertionMarker(GameState state, PlayerId near)
        {
            if (insertionMarker == null)
            {
                return;
            }

            if (_insertionSlot < 0)
            {
                insertionMarker.Hide();
                return;
            }

            int count = state.GetPlayer(near).Board.Count;

            insertionMarker.Show(
                anchors.Board(true),
                BoardDropResolver.GapPosition(count, _insertionSlot, boardSpacing),
                _insertionSlot);
        }

        private void RebuildHand(GameState state, PlayerId seat, bool near)
        {
            Player player = state.GetPlayer(seat);
            Transform anchor = anchors.Hand(near);

            // Only the acting player's cards are turned face up. The other hand
            // stays readable as a count without pretending to be playable.
            bool faceUp = near;
            float scale = handLayout.Scale * (near ? 1f : farHandScale);

            // A card in the air is still in the engine's hand but is no longer
            // in the fan, so the rest of the hand closes up behind it.
            bool holdsDragged = near && !_draggedCard.IsNone && ContainsCard(player, _draggedCard);
            int laidOut = holdsDragged ? player.Hand.Count - 1 : player.Hand.Count;

            int position = 0;

            for (int index = 0; index < player.Hand.Count; index++)
            {
                CardInstance card = player.Hand[index];

                if (!_cardViews.TryGetValue(card.Id, out CardView view) || view == null)
                {
                    view = Instantiate(cardPrefab, anchor);
                    _cardViews[card.Id] = view;
                }

                bool dragged = holdsDragged && card.Id == _draggedCard;

                if (!dragged)
                {
                    // Reparent only when the card is actually somewhere else,
                    // and keep its world position when it moves. A card that has
                    // just been dropped is already under this anchor, holding
                    // the place it was let go of, which is what it has to glide
                    // home from rather than being flung there first.
                    if (view.transform.parent != anchor)
                    {
                        view.transform.SetParent(anchor, true);
                    }

                    CardPose pose = HandFanLayout.GetPose(position, laidOut, handLayout);
                    view.SetRestingPose(pose.LocalPosition, pose.LocalRotation, scale);
                    view.SetHandOrder(position);
                    position++;
                }

                if (faceUp)
                {
                    view.Bind(BuildCardModel(state, card, seat));
                }
                else
                {
                    view.BindFaceDown();
                }
            }
        }

        private static bool ContainsCard(Player player, EntityId card)
        {
            for (int index = 0; index < player.Hand.Count; index++)
            {
                if (player.Hand[index].Id == card)
                {
                    return true;
                }
            }

            return false;
        }

        private CardViewModel BuildCardModel(GameState state, CardInstance card, PlayerId owner)
        {
            CardDefinition definition = state.Catalog.Get(card.CardId);

            // The engine answers whether it is playable, target or no target. A
            // card waiting for the player to aim it is playable; asking about a
            // command with no target in it would dim every targeted card at
            // precisely the moment it became castable.
            bool playable = session.CanPlayCard(owner, card.Id) == RejectionReason.None;

            return new CardViewModel(
                card.Id,
                card.CardId,
                definition.Name,
                Mathf.Max(0, definition.ManaCost + card.CostModifier),
                definition.Attack + card.AttackModifier,
                definition.Health + card.HealthModifier,
                definition.Text,
                definition.Type,
                definition.Class,
                definition.Tribe,
                definition.Rarity,
                playable);
        }

        private MinionViewModel BuildMinionModel(GameState state, Minion minion)
        {
            CardDefinition definition = state.Catalog.Get(minion.CardId);

            bool canAttack =
                state.CurrentPlayer == minion.Controller &&
                session.GetLegalAttackTargets(minion.Controller, minion.Id).Count > 0;

            return new MinionViewModel(
                minion.Id,
                minion.CardId,
                definition.Name,
                minion.Attack,
                minion.CurrentHealth,
                minion.MaxHealth,
                minion.IsDamaged,
                canAttack);
        }

        private void Despawn(EntityId id)
        {
            if (_minionViews.TryGetValue(id, out MinionView minion))
            {
                if (minion != null)
                {
                    Destroy(minion.gameObject);
                }

                _minionViews.Remove(id);
            }

            if (_cardViews.TryGetValue(id, out CardView card))
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }

                _cardViews.Remove(id);
            }
        }

        private void DespawnMissingMinions(GameState state)
        {
            _scratch.Clear();

            foreach (KeyValuePair<EntityId, MinionView> pair in _minionViews)
            {
                if (!state.TryGetEntity(pair.Key, out Entity entity) ||
                    !(entity is Minion minion) ||
                    !minion.IsInPlay)
                {
                    _scratch.Add(pair.Key);
                }
            }

            for (int index = 0; index < _scratch.Count; index++)
            {
                Despawn(_scratch[index]);
            }
        }

        private void DespawnMissingCards(GameState state)
        {
            _scratch.Clear();

            foreach (KeyValuePair<EntityId, CardView> pair in _cardViews)
            {
                if (!state.TryGetEntity(pair.Key, out Entity entity) ||
                    !(entity is CardInstance card) ||
                    card.Zone != ZoneType.Hand)
                {
                    _scratch.Add(pair.Key);
                }
            }

            for (int index = 0; index < _scratch.Count; index++)
            {
                Despawn(_scratch[index]);
            }
        }
    }
}
