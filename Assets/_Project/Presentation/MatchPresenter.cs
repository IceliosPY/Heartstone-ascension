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
    /// It listens to the event queue and, once a batch has been replayed,
    /// reconciles the views against the state: spawn what appeared, remove what
    /// died, and refresh every number. Reconciling rather than mutating blindly
    /// means the scene cannot drift out of step with the rules, which is the one
    /// failure that would be genuinely hard to debug later.
    ///
    /// It is also the only <see cref="IEventVisualizer"/> for now. When
    /// animations arrive, this splits into one visualizer per event and the
    /// reconcile becomes a safety net rather than the main mechanism.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour, IEventVisualizer
    {
        [Header("Wiring")]
        [SerializeField] private GameSession session;
        [SerializeField] private BoardAnchors anchors;
        [SerializeField] private MatchHud hud;

        [Header("Prefabs")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private MinionView minionPrefab;

        [Header("Heroes")]
        [SerializeField] private HeroView playerOneHero;
        [SerializeField] private HeroView playerTwoHero;

        [Header("Layout")]
        [SerializeField] private HandFanSettings handLayout = new HandFanSettings();
        [SerializeField] private float boardSpacing = 1.15f;

        private readonly Dictionary<EntityId, CardView> _cardViews = new Dictionary<EntityId, CardView>();
        private readonly Dictionary<EntityId, MinionView> _minionViews = new Dictionary<EntityId, MinionView>();
        private readonly List<CardView> _faceDownPool = new List<CardView>();
        private readonly List<EntityId> _scratch = new List<EntityId>();

        public GameSession Session => session;

        public HeroView HeroOf(PlayerId player) => player == PlayerId.One ? playerOneHero : playerTwoHero;

        public bool TryGetCardView(EntityId id, out CardView view) => _cardViews.TryGetValue(id, out view);

        public bool TryGetMinionView(EntityId id, out MinionView view) => _minionViews.TryGetValue(id, out view);

        public IReadOnlyDictionary<EntityId, MinionView> MinionViews => _minionViews;

        public IReadOnlyDictionary<EntityId, CardView> CardViews => _cardViews;

        private void OnEnable()
        {
            if (session != null && session.Queue != null)
            {
                session.Queue.AddVisualizer(this);
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
        /// Reacts to one event.
        ///
        /// Removal happens here rather than in the reconcile, so a minion
        /// disappears at the moment its death is reported and not a batch later.
        /// Everything else is left to the reconcile, because with instant
        /// visuals there is nothing to gain from touching a label twice.
        /// </summary>
        public bool Handle(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case MinionDiedEvent died:
                    Despawn(died.MinionId);
                    return true;

                case CardBurnedEvent burned:
                    Despawn(burned.CardInstanceId);
                    return true;

                case GameEndedEvent ended:
                    if (hud != null)
                    {
                        hud.ShowResult(ended.Result);
                    }

                    return true;
            }

            // Everything else is picked up by the reconcile that follows.
            return false;
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

            RebuildBoard(state, PlayerId.One);
            RebuildBoard(state, PlayerId.Two);

            RebuildHand(state, PlayerId.One);
            RebuildHand(state, PlayerId.Two);

            RefreshHeroes(state);

            if (hud != null)
            {
                hud.Refresh(state);
            }
        }

        private void RefreshHeroes(GameState state)
        {
            if (playerOneHero != null)
            {
                playerOneHero.Bind(state.GetPlayer(PlayerId.One).Hero, "PLAYER 1");
            }

            if (playerTwoHero != null)
            {
                playerTwoHero.Bind(state.GetPlayer(PlayerId.Two).Hero, "PLAYER 2");
            }
        }

        private void RebuildBoard(GameState state, PlayerId seat)
        {
            Player player = state.GetPlayer(seat);
            Transform anchor = anchors.BoardOf(seat);

            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                Minion minion = player.Board[slot];

                if (!_minionViews.TryGetValue(minion.Id, out MinionView view) || view == null)
                {
                    view = Instantiate(minionPrefab, anchor);
                    _minionViews[minion.Id] = view;
                }

                view.transform.SetParent(anchor, false);
                view.transform.localPosition = BoardRowLayout.GetPosition(slot, player.Board.Count, boardSpacing);
                view.transform.localRotation = Quaternion.identity;

                view.Bind(BuildMinionModel(state, minion));
            }

            DespawnMissing(_minionViews, state);
        }

        private void RebuildHand(GameState state, PlayerId seat)
        {
            Player player = state.GetPlayer(seat);
            Transform anchor = anchors.HandOf(seat);

            // The player whose turn it is sees their cards; the other hand is
            // shown as backs, so both hands are visible without either being
            // mistaken for the one that can act.
            bool faceUp = state.CurrentPlayer == seat;

            for (int index = 0; index < player.Hand.Count; index++)
            {
                CardInstance card = player.Hand[index];

                if (!_cardViews.TryGetValue(card.Id, out CardView view) || view == null)
                {
                    view = Instantiate(cardPrefab, anchor);
                    _cardViews[card.Id] = view;
                }

                view.transform.SetParent(anchor, false);

                CardPose pose = HandFanLayout.GetPose(index, player.Hand.Count, handLayout);
                view.transform.localPosition = pose.LocalPosition;
                view.transform.localRotation = pose.LocalRotation;
                view.transform.localScale = Vector3.one * pose.Scale;

                if (faceUp)
                {
                    view.Bind(BuildCardModel(state, card, seat));
                }
                else
                {
                    view.BindFaceDown();
                }
            }

            DespawnMissingCards(state);
        }

        private CardViewModel BuildCardModel(GameState state, CardInstance card, PlayerId owner)
        {
            CardDefinition definition = state.Catalog.Get(card.CardId);

            // The engine answers whether it is playable. The view never guesses.
            bool playable = session.CanSubmit(new PlayCardCommand(owner, card.Id));

            return new CardViewModel(
                card.Id,
                card.CardId,
                definition.Name,
                Mathf.Max(0, definition.ManaCost + card.CostModifier),
                definition.Attack + card.AttackModifier,
                definition.Health + card.HealthModifier,
                definition.Text,
                definition.Type,
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

        private void DespawnMissing(Dictionary<EntityId, MinionView> views, GameState state)
        {
            _scratch.Clear();

            foreach (KeyValuePair<EntityId, MinionView> pair in views)
            {
                if (!state.TryGetEntity(pair.Key, out Entity entity) || !(entity is Minion minion) || !minion.IsInPlay)
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
